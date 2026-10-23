using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace WardenId.SharedKernel.Persistence.Audit;

/// <summary>
/// Maps AuditEventRecord to audit.events. WardenId.Audit.Infrastructure's AuditDbContext owns
/// the migration for this table; any other module that starts appending audit rows applies
/// this same configuration but additionally excludes it from its own migrations via
/// modelBuilder.Entity&lt;AuditEventRecord&gt;().ToTable(t => t.ExcludeFromMigrations()) — see
/// docs/audit/PLAN.md §3.1-3.2. audit.events is declaratively partitioned by Channel at the
/// DDL level; Postgres requires the partition key inside any primary key on a partitioned
/// table, hence the composite (Id, Channel) key rather than Id alone.
/// </summary>
public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEventRecord>
{
    public const string Schema = "audit";
    public const string Table = "events";

    public void Configure(EntityTypeBuilder<AuditEventRecord> builder)
    {
        builder.ToTable(Table, Schema);

        builder.HasKey(e => new { e.Id, e.Channel });
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Sequence);
        builder.Property(e => e.Channel).HasMaxLength(30).IsRequired();
        builder.Property(e => e.TenantId);
        builder.Property(e => e.OccurredAt).IsRequired();
        builder.Property(e => e.Payload);
        builder.Property(e => e.LeafHash).IsRequired();

        // Search columns, written alongside the payload (see AuditEventRecord). Actor ids are opaque
        // and unbounded (client ids, external subjects), so Actor/ActorId/Targets stay unbounded text:
        // a length cap would fail the write, and admin.* writes fail closed.
        builder.Property(e => e.EventType).HasMaxLength(100);
        builder.Property(e => e.Actor);
        builder.Property(e => e.ActorId);
        builder.Property(e => e.Targets);
        builder.Property(e => e.Outcome).HasMaxLength(20);

        // Dense-sequence range scans (sealer, phase 3) and tenant/time-scoped queries (phase 4).
        builder.HasIndex(e => new { e.Channel, e.Sequence });
        builder.HasIndex(e => new { e.TenantId, e.OccurredAt });

        // Event list filters. OccurredAt second so a filtered page can be read newest-first from the index.
        builder.HasIndex(e => new { e.EventType, e.OccurredAt });
        builder.HasIndex(e => new { e.Actor, e.OccurredAt });
        builder.HasIndex(e => new { e.ActorId, e.OccurredAt });
        builder.HasIndex(e => new { e.Outcome, e.OccurredAt });
        builder.HasIndex(e => e.Targets).HasMethod(PostgresConventions.IndexMethods.Gin);
    }
}
