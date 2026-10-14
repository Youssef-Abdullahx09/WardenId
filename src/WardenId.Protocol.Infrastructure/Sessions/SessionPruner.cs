using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WardenId.Protocol.Domain.Sessions;
using WardenId.Protocol.Infrastructure.Persistence;

namespace WardenId.Protocol.Infrastructure.Sessions;

public sealed class SessionPruner(ProtocolDbContext dbContext) : ISessionPruner
{
    // Set-based delete bypasses the change tracker: no soft delete, CAP events or audit rows per pruned
    // session. SessionAuthorizations rows go with it through the owned FK's ON DELETE CASCADE.
    public Task PruneAsync(DateTimeOffset threshold, CancellationToken cancellationToken) =>
        dbContext.Sessions.Where(IsPrunable(threshold)).ExecuteDeleteAsync(cancellationToken);

    /// <summary>Ended or expired before <paramref name="threshold"/>. Exposed so tests can check it without Postgres.</summary>
    public static Expression<Func<Session, bool>> IsPrunable(DateTimeOffset threshold) =>
        s => s.EndedAt < threshold || s.ExpiresAt < threshold;
}
