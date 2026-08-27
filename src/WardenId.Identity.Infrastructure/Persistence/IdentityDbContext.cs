using Microsoft.EntityFrameworkCore;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;
using WardenId.SharedKernel.Persistence;
using WardenId.SharedKernel.Persistence.Audit;

namespace WardenId.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext(
    DbContextOptions<IdentityDbContext> options) : DbContextBase(options), IIdentityDbContext
{
    public const string Schema = "Identity";

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);

        // AuditEventConfiguration lives in SharedKernel.Persistence, a different assembly —
        // ApplyConfigurationsFromAssembly above only scans this module's own assembly, so it
        // needs applying explicitly. AuditDbContext owns the migration for audit.events; every
        // other module maps the same entity but excludes it from its own migrations.
        modelBuilder.ApplyConfiguration(new AuditEventConfiguration());
        modelBuilder.Entity<AuditEventRecord>().ToTable("events", "audit", t => t.ExcludeFromMigrations());

        base.OnModelCreating(modelBuilder);
    }
}
