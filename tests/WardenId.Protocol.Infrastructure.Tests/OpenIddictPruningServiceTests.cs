using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using WardenId.Protocol.Infrastructure.OpenIddict;

namespace WardenId.Protocol.Infrastructure.Tests;

public class OpenIddictPruningServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    private sealed class RecordingPruner : IOpenIddictPruner
    {
        public SemaphoreSlim Calls { get; } = new(0);
        public List<DateTimeOffset> Thresholds { get; } = [];
        public bool Throw { get; set; }
        public bool ThrowCanceled { get; set; }

        public Task PruneAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
        {
            lock (Thresholds) Thresholds.Add(threshold);
            Calls.Release();
            if (ThrowCanceled) return Task.FromException(new OperationCanceledException("db command timed out"));
            return Throw ? Task.FromException(new InvalidOperationException("db down")) : Task.CompletedTask;
        }
    }

    private static (OpenIddictPruningService Service, RecordingPruner Pruner, FakeTimeProvider Clock) Create(bool enabled = true)
    {
        var pruner = new RecordingPruner();
        var services = new ServiceCollection().AddSingleton<IOpenIddictPruner>(pruner).BuildServiceProvider();
        var clock = new FakeTimeProvider(Start);
        var options = Options.Create(new ProtocolOpenIddictPruningOptions
        {
            PruningEnabled = enabled,
            PruneInterval = TimeSpan.FromHours(1),
            PruneMinimumAge = TimeSpan.FromDays(14)
        });
        var service = new OpenIddictPruningService(
            services.GetRequiredService<IServiceScopeFactory>(), options, clock, NullLogger<OpenIddictPruningService>.Instance);
        return (service, pruner, clock);
    }

    private static async Task WaitForCallAsync(RecordingPruner pruner) =>
        Assert.True(await pruner.Calls.WaitAsync(TimeSpan.FromSeconds(5)), "PruneAsync was not called.");

    [Fact]
    public async Task PrunesOnStart_ThenEveryInterval_WithMinimumAgeThreshold()
    {
        var (service, pruner, clock) = Create();
        await service.StartAsync(CancellationToken.None);

        await WaitForCallAsync(pruner);
        clock.Advance(TimeSpan.FromHours(1));
        await WaitForCallAsync(pruner);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(Start - TimeSpan.FromDays(14), pruner.Thresholds[0]);
        Assert.Equal(Start + TimeSpan.FromHours(1) - TimeSpan.FromDays(14), pruner.Thresholds[1]);
    }

    [Fact]
    public async Task Disabled_NeverPrunes()
    {
        var (service, pruner, _) = Create(enabled: false);
        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.False(await pruner.Calls.WaitAsync(TimeSpan.FromMilliseconds(200)));
    }

    [Fact]
    public async Task Failure_IsLogged_AndLoopContinues()
    {
        var (service, pruner, clock) = Create();
        pruner.Throw = true;
        await service.StartAsync(CancellationToken.None);

        await WaitForCallAsync(pruner);
        clock.Advance(TimeSpan.FromHours(1));
        await WaitForCallAsync(pruner);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(2, pruner.Thresholds.Count);
    }

    [Fact]
    public async Task NonShutdownCancellation_IsLogged_AndLoopContinues()
    {
        var (service, pruner, clock) = Create();
        pruner.ThrowCanceled = true;
        await service.StartAsync(CancellationToken.None);

        await WaitForCallAsync(pruner);
        clock.Advance(TimeSpan.FromHours(1));
        await WaitForCallAsync(pruner);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(2, pruner.Thresholds.Count);
    }
}
