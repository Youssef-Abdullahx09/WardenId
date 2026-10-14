using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WardenId.Protocol.Application;
using WardenId.Protocol.Infrastructure.OpenIddict;

namespace WardenId.Protocol.Infrastructure.Sessions;

/// <summary>
/// Deletes session rows ended or expired longer than ProtocolSession:PruneMinimumAge ago, on start and every
/// ProtocolOpenIddict:PruneInterval (one pruning switch for the module). Idempotent, so several Gateway
/// instances may run it. A failed run is logged and retried next tick — it never takes the host down.
/// </summary>
public sealed class SessionPruningService(
    IServiceScopeFactory scopeFactory,
    IOptions<ProtocolOpenIddictPruningOptions> pruningOptions,
    IOptions<ProtocolSessionOptions> sessionOptions,
    TimeProvider timeProvider,
    ILogger<SessionPruningService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pruning = pruningOptions.Value;
        if (!pruning.PruningEnabled) return;

        var minimumAge = sessionOptions.Value.PruneMinimumAge;
        using var timer = new PeriodicTimer(pruning.PruneInterval, timeProvider);
        try
        {
            do
            {
                await PruneOnceAsync(minimumAge, stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown.
        }
    }

    private async Task PruneOnceAsync(TimeSpan minimumAge, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var pruner = scope.ServiceProvider.GetRequiredService<ISessionPruner>();
            await pruner.PruneAsync(timeProvider.GetUtcNow() - minimumAge, stoppingToken);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Pruning ended and expired sessions failed; retrying next interval.");
        }
    }
}
