using WardenId.Protocol.Domain.Sessions;
using WardenId.Protocol.Infrastructure.Sessions;

namespace WardenId.Protocol.Infrastructure.Tests.Sessions;

// The predicate is evaluated in memory: EF InMemory has no ExecuteDelete, and Npgsql translates the same expression.
public class SessionPrunerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Threshold = Now.AddDays(-30);

    private static Session Started(DateTimeOffset authTime, TimeSpan lifetime) =>
        Session.Start(Guid.NewGuid(), Guid.NewGuid(), authTime, authTime + lifetime).Value;

    [Fact]
    public void IsPrunable_SelectsOnlySessionsEndedOrExpiredBeforeThreshold()
    {
        var live = Started(Now.AddHours(-1), TimeSpan.FromDays(1));
        var endedRecently = Started(Now.AddHours(-2), TimeSpan.FromDays(1));
        endedRecently.End(EndReason.UserLogout, Now.AddHours(-1));
        var expiredRecently = Started(Now.AddDays(-2), TimeSpan.FromDays(1));
        var expiredLongAgo = Started(Now.AddDays(-40), TimeSpan.FromDays(1));
        var endedLongAgoNotExpired = Started(Now.AddDays(-35), TimeSpan.FromDays(60));
        endedLongAgoNotExpired.End(EndReason.AdminEnded, Now.AddDays(-31));

        var prunable = new[] { live, endedRecently, expiredRecently, expiredLongAgo, endedLongAgoNotExpired }
            .AsQueryable()
            .Where(SessionPruner.IsPrunable(Threshold))
            .Select(s => s.Id)
            .ToList();

        Assert.Equal(new[] { expiredLongAgo.Id, endedLongAgoNotExpired.Id }, prunable);
    }

    [Fact]
    public void IsPrunable_ExpiringExactlyAtThreshold_IsKept()
    {
        var session = Started(Threshold.AddDays(-1), TimeSpan.FromDays(1));

        Assert.False(SessionPruner.IsPrunable(Threshold).Compile()(session));
    }
}
