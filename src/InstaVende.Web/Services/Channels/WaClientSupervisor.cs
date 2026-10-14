using System.Diagnostics;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Options;

namespace InstaVende.Web.Services.Channels;

/// <summary>
/// Lanza y supervisa el proceso Node wa-client:
/// <list type="bullet">
///   <item>Arranca cuando la app ya escucha (no bloquea el inicio de Kestrel).</item>
///   <item>Le pasa por entorno la URL de retorno, el puerto y el secreto compartido.</item>
///   <item>Lo reinicia con backoff si termina o deja de responder al health check.</item>
/// </list>
/// </summary>
public sealed class WaClientSupervisor : BackgroundService
{
    private static readonly TimeSpan[] Backoff = [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60)];

    private readonly WhatsAppClientOptions _opts;
    private readonly WhatsAppClientApi _api;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IServer _server;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<WaClientSupervisor> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private Process? _process;
    private int _consecutiveFailures;
    private int _restarts;
    private bool _externalWarned;

    public WaClientSupervisor(IOptions<WhatsAppClientOptions> opts, WhatsAppClientApi api, IHostApplicationLifetime lifetime,
        IServer server, IWebHostEnvironment env, ILogger<WaClientSupervisor> logger)
    {
        _opts = opts.Value;
        _api = api;
        _lifetime = lifetime;
        _server = server;
        _env = env;
        _logger = logger;
    }

    /// <summary>true mientras se está (re)lanzando el proceso.</summary>
    public bool IsRestarting => _lock.CurrentCount == 0;
    public int ConsecutiveFailures => _consecutiveFailures;
    public bool IsManaged => _opts.AutoStart;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opts.AutoStart)
        {
            _logger.LogInformation("wa-client: AutoStart desactivado; se asume un proceso gestionado externamente.");
            return;
        }

        // Esperar a que Kestrel esté escuchando para conocer la URL de retorno
        var started = new TaskCompletionSource();
        using (_lifetime.ApplicationStarted.Register(() => started.TrySetResult()))
        using (stoppingToken.Register(() => started.TrySetCanceled()))
        {
            try { await started.Task; } catch (TaskCanceledException) { return; }
        }

        var interval = TimeSpan.FromSeconds(_opts.HealthCheckIntervalSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            var running   = _process is { HasExited: false };
            var reachable = await _api.IsHealthyAsync(stoppingToken);
            if (reachable)
            {
                _consecutiveFailures = 0;
                _restarts = 0;
                if (!running && !_externalWarned)
                {
                    _externalWarned = true;
                    _logger.LogWarning("wa-client ya está corriendo fuera de esta app en {Url}; no se lanzará otro proceso.", _opts.BaseUrl);
                }
            }
            else if (!running || ++_consecutiveFailures >= _opts.UnhealthyThreshold)
            {
                if (_restarts > 0) await Task.Delay(Backoff[Math.Min(_restarts - 1, Backoff.Length - 1)], stoppingToken);
                await RestartAsync(stoppingToken);
                _restarts++;
            }

            try { await Task.Delay(interval, stoppingToken); } catch (TaskCanceledException) { break; }
        }
    }

    /// <summary>Fuerza un reinicio inmediato del proceso (acción "Reiniciar servicio" del panel).</summary>
    public async Task RestartAsync(CancellationToken ct = default)
    {
        if (!_opts.AutoStart) return;
        if (!await _lock.WaitAsync(0, ct)) return; // ya hay un reinicio en curso
        try
        {
            StopProcess();
            Launch();
            _consecutiveFailures = 0;
        }
        finally
        {
            _lock.Release();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        StopProcess();
    }

    public override void Dispose()
    {
        StopProcess();
        _lock.Dispose();
        base.Dispose();
    }

    private void Launch()
    {
        var path = _opts.ResolveClientPath(_env.ContentRootPath);
        if (!File.Exists(Path.Combine(path, "index.js")))
        {
            _logger.LogError("wa-client: no se encontró index.js en {Path}. Revisa WhatsAppClient:ClientPath.", path);
            return;
        }
        if (!Directory.Exists(Path.Combine(path, "node_modules")))
        {
            _logger.LogError("wa-client: faltan dependencias. Ejecuta 'npm ci' en {Path}.", path);
            return;
        }

        var baseUri = new Uri(_opts.BaseUrl);
        var psi = new ProcessStartInfo("node", "index.js")
        {
            WorkingDirectory       = path,
            UseShellExecute        = false,
            CreateNoWindow         = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
        };
        psi.Environment["WA_PORT"]           = baseUri.Port.ToString();
        psi.Environment["WA_HOST"]           = baseUri.Host;
        psi.Environment["DOTNET_BASE_URL"]   = ResolveCallbackUrl();
        psi.Environment["WA_SHARED_SECRET"]  = _opts.SharedSecret;
        if (!string.IsNullOrWhiteSpace(_opts.SessionDir))
            psi.Environment["WA_SESSION_DIR"] = Path.GetFullPath(Path.Combine(_env.ContentRootPath, _opts.SessionDir));

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) _logger.LogInformation("[wa-client] {Line}", e.Data); };
        process.ErrorDataReceived  += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) _logger.LogWarning("[wa-client] {Line}", e.Data); };
        process.Exited += (_, _) => _logger.LogWarning("wa-client terminó (código {Code}); se reiniciará automáticamente.", SafeExitCode(process));

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            _process = process;
            _logger.LogInformation("wa-client lanzado (PID {Pid}) desde {Path}", process.Id, path);
        }
        catch (Exception ex)
        {
            process.Dispose();
            _logger.LogError(ex, "No se pudo lanzar wa-client. ¿Está Node.js instalado y en el PATH?");
        }
    }

    private void StopProcess()
    {
        var p = Interlocked.Exchange(ref _process, null);
        if (p == null) return;
        try { if (!p.HasExited) p.Kill(entireProcessTree: true); }
        catch (Exception ex) { _logger.LogDebug(ex, "Error deteniendo wa-client"); }
        finally { p.Dispose(); }
    }

    private string ResolveCallbackUrl()
    {
        if (!string.IsNullOrWhiteSpace(_opts.CallbackBaseUrl)) return _opts.CallbackBaseUrl.TrimEnd('/');
        var addresses = _server.Features.Get<IServerAddressesFeature>()?.Addresses ?? [];
        var http = addresses.FirstOrDefault(a => a.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) ?? addresses.FirstOrDefault();
        return (http ?? "http://localhost:5221").Replace("://+", "://localhost").Replace("://*", "://localhost").Replace("://[::]", "://localhost").Replace("://0.0.0.0", "://localhost").TrimEnd('/');
    }

    private static string SafeExitCode(Process p)
    {
        try { return p.ExitCode.ToString(); } catch { return "?"; }
    }
}
