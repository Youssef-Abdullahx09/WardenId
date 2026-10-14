using Microsoft.EntityFrameworkCore;
using WardenId.Protocol.Application.Abstractions;
using WardenId.Protocol.Domain.Sessions;
using WardenId.Protocol.Infrastructure.Persistence.Configurations;
using WardenId.SharedKernel.Persistence;
using WardenId.SharedKernel.Persistence.Audit;

namespace WardenId.Protocol.Infrastructure.Persistence;

/// <summary>
/// Protocol's aggregates (login sessions) in schema "Protocol". Unlike <c>OpenIddictDbContext</c>
/// it is registered with the shared interceptors, so Session domain events reach the CAP outbox.
/// </summary>
public sealed class ProtocolDbContext(DbContextOptions<ProtocolDbContext> options)
    : DbContextBase(options), IProtocolDbContext
{
    public const string Schema = "Protocol";

    public DbSet<Session> Sessions => Set<Session>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        // Explicit rather than an assembly scan: this assembly also holds OpenIddictDbContext.
        modelBuilder.ApplyConfiguration(new SessionConfiguration());

        // AuditDbContext owns the migration for audit.events; every other module maps the same entity
        // (the audit interceptor adds rows to this context) but excludes it from its own migrations.
        modelBuilder.ApplyConfiguration(new AuditEventConfiguration());
        modelBuilder.Entity<AuditEventRecord>().ToTable("events", "audit", t => t.ExcludeFromMigrations());

        base.OnModelCreating(modelBuilder);
    }
}
