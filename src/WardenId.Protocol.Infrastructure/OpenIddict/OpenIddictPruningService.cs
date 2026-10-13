using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WardenId.Protocol.Infrastructure.OpenIddict;

/// <summary>
/// Codes, rotated/revoked refresh tokens and their authorizations accumulate one row per event; this
/// prunes them on start and every PruneInterval. Idempotent, so several Gateway instances may run it.
/// A failed run is logged and retried next tick — it never takes the host down.
/// </summary>
public sealed class OpenIddictPruningService(
    IServiceScopeFactory scopeFactory,
    IOptions<ProtocolOpenIddictPruningOptions> options,
    TimeProvider timeProvider,
    ILogger<OpenIddictPruningService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.PruningEnabled) return;

        using var timer = new PeriodicTimer(settings.PruneInterval, timeProvider);
        try
        {
            do
            {
                await PruneOnceAsync(settings.PruneMinimumAge, stoppingToken);
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
            var pruner = scope.ServiceProvider.GetRequiredService<IOpenIddictPruner>();
            await pruner.PruneAsync(timeProvider.GetUtcNow() - minimumAge, stoppingToken);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Pruning OpenIddict tokens and authorizations failed; retrying next interval.");
        }
    }
}
