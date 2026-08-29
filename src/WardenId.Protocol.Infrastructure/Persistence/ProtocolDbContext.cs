using Microsoft.EntityFrameworkCore;
using WardenId.Protocol.Domain;
using WardenId.SharedKernel.Persistence;
using WardenId.SharedKernel.Persistence.Audit;

namespace WardenId.Protocol.Infrastructure.Persistence;

public sealed class ProtocolDbContext(
    DbContextOptions<ProtocolDbContext> options) : DbContextBase(options)
{
    public const string Schema = "Protocol";

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProtocolDbContext).Assembly);

        // AuditEventConfiguration lives in SharedKernel.Persistence, a different assembly —
        // ApplyConfigurationsFromAssembly above only scans this module's own assembly, so it
        // needs applying explicitly. AuditDbContext owns the migration for audit.events; every
        // other module maps the same entity but excludes it from its own migrations.
        modelBuilder.ApplyConfiguration(new AuditEventConfiguration());
        modelBuilder.Entity<AuditEventRecord>().ToTable("events", "audit", t => t.ExcludeFromMigrations());

        base.OnModelCreating(modelBuilder);
    }
}
