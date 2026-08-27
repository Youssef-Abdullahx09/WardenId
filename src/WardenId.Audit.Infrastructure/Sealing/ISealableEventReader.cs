using WardenId.Audit.Domain;
using WardenId.Audit.Infrastructure.Persistence;
using WardenId.SharedKernel.Persistence.Audit;

namespace WardenId.Audit.Infrastructure.Sealing;

/// <summary>
/// I4 — "seal below the snapshot watermark": only rows whose xmin is below
/// pg_snapshot_xmin(pg_current_snapshot()) are guaranteed not to be followed by an
/// in-flight, not-yet-committed transaction. Behind an interface so
/// <see cref="AuditSealerService"/>'s dense-sequencing/chaining/policy logic can be unit
/// tested against a fake (and the InMemory provider, which can't run the raw SQL the real
/// implementation needs) — see PgSealableEventReader's own doc comment.
/// </summary>
public interface ISealableEventReader
{
    Task<IReadOnlyList<AuditEventRecord>> GetSealableEventsAsync(
        AuditDbContext db, AuditChannel channel, Guid? tenantId, int limit, CancellationToken cancellationToken = default);
}
