using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace InstaVende.Web.Services;

/// <summary>
/// Hosted service that auto-starts the local whatsapp-web.js Node process
/// when the ASP.NET app boots, and keeps a reference to stop it on shutdown.
/// Improvements:
///   1. Redirects Node stdout/stderr into the .NET logger so crashes are visible.
///   2. Watches for unexpected Node exit and logs a warning (no auto-restart —
///      the Node process manages its own WhatsApp reconnection internally).
///   3. Increases the reachability wait to 15 s to accommodate Puppeteer startup.
/// </summary>
public sealed class WaClientHostedService : IHostedService, IDisposable
{
    private readonly WhatsAppClientOptions _waOpts;
    private readonly ILogger<WaClientHostedService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private Process? _process;

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

        var waUrl   = _waOpts.BaseUrl;
        var rawPath = _waOpts.ClientPath;
        var waPath  = Path.IsPathRooted(rawPath)
            ? rawPath
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, rawPath));

        _logger.LogInformation("WaClientHostedService: resolved wa-client path ? {Path}", waPath);

        // If already reachable, skip launch
        if (await IsReachableAsync(waUrl))
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

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName               = "node",
                Arguments              = "index.js",
                WorkingDirectory       = waPath,
                UseShellExecute        = false,
                CreateNoWindow         = true,
                // ?? Improvement 1: capture Node output into .NET logger ??????
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

            // Improvement 2: warn on unexpected Node exit
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

            // Improvement 3: wait up to 15 s — Puppeteer/Chrome needs time
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
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

    public Task StopAsync(CancellationToken cancellationToken)
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
        return Task.CompletedTask;
    }

    public void Dispose() => _process?.Dispose();

    private async Task<bool> IsReachableAsync(string baseUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient("wa-health");
            using var resp   = await client.GetAsync($"{baseUrl}/health", cancellationToken);
            return resp.IsSuccessStatusCode;
        }
        catch (OperationCanceledException) { throw; }
        catch { return false; }
    }
}
