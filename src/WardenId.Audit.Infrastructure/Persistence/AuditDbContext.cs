using Microsoft.EntityFrameworkCore;
using WardenId.Audit.Infrastructure.Sealing;
using WardenId.SharedKernel.Persistence;
using WardenId.SharedKernel.Persistence.Audit;

namespace WardenId.Audit.Infrastructure.Persistence;

/// <summary>
/// Owns the audit.events migration. Deliberately does not register DomainEventsInterceptor —
/// AuditEventRecord never raises domain events and audit writes must never depend on CAP's
/// outbox (spec §5); registering it here would open a CAP-participating transaction on every
/// single insert for no reason.
/// </summary>
public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContextBase(options)
{
    public const string Schema = "audit";

    public DbSet<AuditEventRecord> AuditEvents => Set<AuditEventRecord>();
    public DbSet<AuditSegmentRecord> AuditSegments => Set<AuditSegmentRecord>();
    public DbSet<AuditCheckpointRecord> AuditCheckpoints => Set<AuditCheckpointRecord>();
    public DbSet<AuditCheckpointSigningKeyRecord> AuditCheckpointSigningKeys => Set<AuditCheckpointSigningKeyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        // AuditEventConfiguration lives in SharedKernel.Persistence, a different assembly —
        // neither this context's own assembly-scan nor the base class's can see it, so it's
        // applied explicitly.
        modelBuilder.ApplyConfiguration(new AuditEventConfiguration());
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
