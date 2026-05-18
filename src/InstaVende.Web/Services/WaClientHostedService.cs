using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace InstaVende.Web.Services;

/// <summary>
/// Hosted service that auto-starts the local whatsapp-web.js Node process
/// when the ASP.NET app boots, and keeps a reference to stop it on shutdown.
/// Improvements:
///   1. Redirects Node stdout/stderr into the .NET logger so crashes are visible.
///   2. Watches for unexpected Node exit and logs a warning.
///   3. Increases the reachability wait to 15 s to accommodate Puppeteer startup.
///   4. Exposes <see cref="RestartIfDeadAsync"/> so <c>WaStatus</c> polling can
///      trigger an automatic Node restart when the process is found to be offline.
/// </summary>
public class WaClientHostedService : IHostedService, IDisposable
{
    private readonly WhatsAppClientOptions _waOpts;
    private readonly ILogger<WaClientHostedService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private Process? _process;

    // Guards against launching two Node processes simultaneously.
    private readonly SemaphoreSlim _restartLock = new(1, 1);

    /// <summary>
    /// <c>true</c> mientras <see cref="RestartIfDeadAsync"/> mantiene el semáforo
    /// (proceso de reinicio en curso). La UI puede consultarlo vía
    /// <c>GET /ChannelConfig/WaRestartStatus</c> para mostrar "Reiniciando servicio…"
    /// en lugar del spinner genérico.
    /// </summary>
    public virtual bool IsRestarting => _restartLock.CurrentCount == 0;

    public WaClientHostedService(
        IOptions<WhatsAppClientOptions> waOptions,
        ILogger<WaClientHostedService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _waOpts = waOptions.Value;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_waOpts.AutoStart)
        {
            _logger.LogInformation("WaClientHostedService: AutoStart disabled, skipping.");
            return;
        }

        await LaunchNodeAsync(cancellationToken);
    }

    /// <summary>
    /// Called by <c>ChannelConfigController.WaStatus</c> when repeated polls
    /// confirm Node is offline. Skips restart if the process is already running
    /// or another restart is already in progress.
    /// </summary>
    public virtual async Task RestartIfDeadAsync(CancellationToken cancellationToken = default)
    {
        if (!_waOpts.AutoStart) return;

        // Non-blocking: if a restart is already running, skip this call.
        if (!await _restartLock.WaitAsync(0, cancellationToken)) return;
        try
        {
            var waUrl = _waOpts.BaseUrl;

            // Re-check reachability before killing anything.
            if (await IsReachableAsync(waUrl, cancellationToken))
            {
                _logger.LogDebug("WaClientHostedService: RestartIfDead — Node is already up, skipping.");
                return;
            }

            _logger.LogWarning("WaClientHostedService: Node appears offline — attempting auto-restart.");

            // Kill the previous process if it is still lingering.
            if (_process is { HasExited: false })
            {
                try { _process.Kill(entireProcessTree: true); }
                catch (Exception ex) { _logger.LogDebug(ex, "WaClientHostedService: error killing old process."); }
            }
            _process?.Dispose();
            _process = null;

            await LaunchNodeAsync(cancellationToken, waitSeconds: 20);
        }
        finally
        {
            _restartLock.Release();
        }
    }

    /// <summary>
    /// Lanza Node si no está ya corriendo. Usado por <c>WaRestart</c> del controlador
    /// para garantizar que el proceso quede registrado en <see cref="_process"/>.
    /// </summary>
    public virtual async Task EnsureRunningAsync(CancellationToken cancellationToken = default)
    {
        // Si ya hay un restart en curso, no hacemos nada
        if (!await _restartLock.WaitAsync(0, cancellationToken)) return;
        try
        {
            var waUrl = _waOpts.BaseUrl;
            if (await IsReachableAsync(waUrl, cancellationToken))
            {
                _logger.LogDebug("WaClientHostedService: EnsureRunning — Node already up.");
                return;
            }
            KillProcess(); // limpia cualquier proceso zombie anterior
            await LaunchNodeAsync(cancellationToken, waitSeconds: 0); // no esperar — el poll lo detectará
        }
        finally
        {
            _restartLock.Release();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        KillProcess();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Detiene el proceso Node en curso (si existe) sin tocar la sesión.
    /// Usado por <c>ClearWaSession</c> para garantizar que ningún proceso Chrome
    /// tenga abiertos los archivos de sesión antes de eliminarlos.
    /// </summary>
    public void StopNode()
    {
        KillProcess();
    }

    private void KillProcess()
    {
        if (_process is { HasExited: false })
        {
            try
            {
                _process.Kill(entireProcessTree: true);
                _logger.LogInformation("WaClientHostedService: wa-client process stopped.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "WaClientHostedService: Error stopping wa-client process.");
            }
        }
        _process?.Dispose();
        _process = null;
    }

    public void Dispose()
    {
        _process?.Dispose();
        _restartLock.Dispose();
    }

    // ?? Private helpers ???????????????????????????????????????????????????????

    /// <summary>
    /// Resolves the wa-client path, launches <c>node index.js</c> and waits up
    /// to <paramref name="waitSeconds"/> seconds for the /health endpoint to respond.
    /// </summary>
    private async Task LaunchNodeAsync(CancellationToken cancellationToken, int waitSeconds = 15)
    {
        var waUrl   = _waOpts.BaseUrl;
        var rawPath = _waOpts.ClientPath;
        var waPath  = Path.IsPathRooted(rawPath)
            ? rawPath
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, rawPath));

        try
        {
            _logger.LogInformation("WaClientHostedService: resolved wa-client path ? {Path}", waPath);

            // If already reachable, skip launch
            if (await IsReachableAsync(waUrl, cancellationToken))
            {
                _logger.LogInformation("WaClientHostedService: wa-client already running at {Url}", waUrl);
                return;
            }

            if (!Directory.Exists(waPath))
            {
                _logger.LogWarning("WaClientHostedService: wa-client path not found: {Path}", waPath);
                return;
            }

            var indexJs = Path.Combine(waPath, "index.js");
            if (!File.Exists(indexJs))
            {
                _logger.LogWarning("WaClientHostedService: index.js not found in {Path}", waPath);
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName               = "node",
                Arguments              = "index.js",
                WorkingDirectory       = waPath,
                UseShellExecute        = false,
                CreateNoWindow         = true,
                // Capture Node output into .NET logger
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };

            _process = new Process { StartInfo = psi, EnableRaisingEvents = true };

            // Pipe Node stdout/stderr ? ILogger (non-blocking async reads)
            _process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    _logger.LogInformation("[wa-client] {Line}", e.Data);
            };
            _process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    _logger.LogWarning("[wa-client] {Line}", e.Data);
            };

            // Warn on unexpected Node exit
            _process.Exited += (sender, _) =>
            {
                if (sender is Process p)
                {
                    if (p.ExitCode != 0)
                        _logger.LogWarning(
                            "WaClientHostedService: wa-client exited unexpectedly with code {Code}.",
                            p.ExitCode);
                    else
                        _logger.LogInformation("WaClientHostedService: wa-client process exited.");
                }
            };

            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            _logger.LogInformation("WaClientHostedService: Launched wa-client (PID {Pid}) from {Path}",
                _process.Id, waPath);

            // Wait up to waitSeconds — Puppeteer/Chrome needs time
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(waitSeconds));
            using var linkedCts  = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            try
            {
                while (!linkedCts.Token.IsCancellationRequested)
                {
                    await Task.Delay(1000, linkedCts.Token);
                    if (await IsReachableAsync(waUrl, linkedCts.Token))
                    {
                        _logger.LogInformation("WaClientHostedService: wa-client is up at {Url}", waUrl);
                        return;
                    }
                }
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                // Timeout reached — Puppeteer may still be initializing
            }
            _logger.LogInformation(
                "WaClientHostedService: wa-client launched but not yet responding — Puppeteer may still be initializing.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WaClientHostedService: Failed to launch wa-client");
        }
    }

    private async Task<bool> IsReachableAsync(string baseUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient("wa-health");
            using var resp   = await client.GetAsync($"{baseUrl}/health", cancellationToken);
            return resp.IsSuccessStatusCode;
        }
        // Solo relanzar si fue el token externo quien canceló (p. ej. shutdown del host).
        // Un TaskCanceledException por timeout del HttpClient NO debe tumbar el arranque.
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return false; }
    }
}
