using WardenId.Protocol.Domain.Sessions;

namespace WardenId.Protocol.Application.Tests.Fakes;

public static class SessionBuilder
{
    public static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Live at <see cref="Now"/>: started at <paramref name="authTime"/> (default an hour ago), one-day lifetime.</summary>
    public static Session Live(Guid tenantId, Guid userId, DateTimeOffset? authTime = null)
    {
        var start = authTime ?? Now.AddHours(-1);
        return Session.Start(tenantId, userId, start, start.AddDays(1)).Value;
    }

    public static Session Ended(Guid tenantId, Guid userId, EndReason reason = EndReason.UserLogout)
    {
        var session = Live(tenantId, userId);
        session.End(reason, Now.AddMinutes(-10));
        return session;
    }

    public static Session Expired(Guid tenantId, Guid userId) =>
        Session.Start(tenantId, userId, Now.AddDays(-2), Now.AddDays(-1)).Value;

    /// <summary>Saves the sessions into a fresh InMemory database and returns its name.</summary>
    public static async Task<string> SeedAsync(params Session[] sessions)
    {
        var databaseName = Guid.NewGuid().ToString();
        await using var db = TestProtocolDbContext.CreateNew(databaseName);
        db.Sessions.AddRange(sessions);
        await db.SaveChangesAsync();
        return databaseName;
    }
}
