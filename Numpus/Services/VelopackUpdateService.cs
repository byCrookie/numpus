using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Logging;
using Velopack.Sources;

namespace Numpus.Services;

public sealed class VelopackUpdateService : IHostedService
{
    private readonly ILogger<VelopackUpdateService> _logger;
    private CancellationTokenSource? _linkedCts;
    private Task? _updateTask;

    public VelopackUpdateService(ILogger<VelopackUpdateService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _updateTask = Task.Run(() => RunUpdateAsync(_linkedCts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var cts = Interlocked.Exchange(ref _linkedCts, null);
        cts?.Cancel();

        if (_updateTask is null)
        {
            cts?.Dispose();
            return;
        }

        try
        {
            await Task.WhenAny(_updateTask, Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Host shutdown is cancelling the wait; nothing to do.
        }
        finally
        {
            cts?.Dispose();
        }
    }

    private async Task RunUpdateAsync(CancellationToken cancellationToken)
    {
        try
        {
            var manager = new UpdateManager(new GithubSource("https://github.com/byCrookie/numpus", GetAccessToken(), prerelease: false));

            if (!manager.IsInstalled)
            {
                _logger.LogDebug("Velopack update skipped because the app is not running from an installed bundle.");
                LogToVelopack(VelopackLogLevel.Debug, "Update skipped; app is running unpackaged.");
                return;
            }

            _logger.LogInformation("Checking for updates via Velopack...");
            LogToVelopack(VelopackLogLevel.Information, "Checking for updates...");
            LogToVelopack(VelopackLogLevel.Information, $"Current version: {manager.CurrentVersion?.ToFullString() ?? "(not installed)"}");

            if (manager.UpdatePendingRestart is not null)
            {
                LogToVelopack(VelopackLogLevel.Information, "An update is pending restart.");
            }

            var updateInfo = await manager.CheckForUpdatesAsync().ConfigureAwait(false);

            if (updateInfo is null)
            {
                _logger.LogInformation("No updates available from GitHub releases.");
                LogToVelopack(VelopackLogLevel.Information, "No updates available.");
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation("Downloading update {Version}...", updateInfo.TargetFullRelease.Version);
            LogToVelopack(VelopackLogLevel.Information, $"Downloading update {updateInfo.TargetFullRelease.Version}...");
            await manager.DownloadUpdatesAsync(updateInfo, cancelToken: cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation("Applying update {Version} and restarting.", updateInfo.TargetFullRelease.Version);
            LogToVelopack(VelopackLogLevel.Information, $"Applying update {updateInfo.TargetFullRelease.Version} and restarting...");
            manager.ApplyUpdatesAndRestart(updateInfo);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Velopack update task cancelled.");
            LogToVelopack(VelopackLogLevel.Debug, "Update check cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Velopack auto-update failed.");
            LogToVelopack(VelopackLogLevel.Error, "Velopack auto-update failed.", ex);
        }
    }

    private static string? GetAccessToken()
    {
        var token = Environment.GetEnvironmentVariable("VELOPACK_GITHUB_TOKEN");
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }

    private static void LogToVelopack(VelopackLogLevel level, string message, Exception? exception = null)
    {
        Program.VelopackLog.Log(level, message, exception);
    }
}
