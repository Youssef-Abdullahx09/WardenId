using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using WardenId.Protocol.Infrastructure.OpenIddict;
using WardenId.Protocol.Infrastructure.Sessions;

namespace WardenId.Protocol.Infrastructure.Tests.Sessions;

public class SessionPruningServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    private sealed class RecordingPruner : ISessionPruner
    {
        public SemaphoreSlim Calls { get; } = new(0);
        public List<DateTimeOffset> Thresholds { get; } = [];
        public bool Throw { get; set; }

        public Task PruneAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
        {
            lock (Thresholds) Thresholds.Add(threshold);
            Calls.Release();
            return Throw ? Task.FromException(new InvalidOperationException("db down")) : Task.CompletedTask;
        }
    }

    private static (SessionPruningService Service, RecordingPruner Pruner, FakeTimeProvider Clock) Create(bool enabled = true)
    {
        var pruner = new RecordingPruner();
        var services = new ServiceCollection().AddSingleton<ISessionPruner>(pruner).BuildServiceProvider();
        var clock = new FakeTimeProvider(Start);
        // OpenIddict's PruneMinimumAge (90 days) must not leak into the session threshold.
        var pruningOptions = Options.Create(new ProtocolOpenIddictPruningOptions
        {
            PruningEnabled = enabled,
            PruneInterval = TimeSpan.FromHours(1),
            PruneMinimumAge = TimeSpan.FromDays(90)
        });
        var sessionOptions = Options.Create(new ProtocolSessionOptions { PruneMinimumAge = TimeSpan.FromDays(30) });
        var service = new SessionPruningService(
            services.GetRequiredService<IServiceScopeFactory>(),
            pruningOptions,
            sessionOptions,
            clock,
            NullLogger<SessionPruningService>.Instance);
        return (service, pruner, clock);
    }

    private static async Task WaitForCallAsync(RecordingPruner pruner) =>
        Assert.True(await pruner.Calls.WaitAsync(TimeSpan.FromSeconds(5)), "PruneAsync was not called.");

    [Fact]
    public async Task PrunesOnStart_ThenEveryInterval_WithSessionMinimumAgeThreshold()
    {
        var (service, pruner, clock) = Create();
        await service.StartAsync(CancellationToken.None);

        await WaitForCallAsync(pruner);
        clock.Advance(TimeSpan.FromHours(1));
        await WaitForCallAsync(pruner);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(Start - TimeSpan.FromDays(30), pruner.Thresholds[0]);
        Assert.Equal(Start + TimeSpan.FromHours(1) - TimeSpan.FromDays(30), pruner.Thresholds[1]);
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
}
