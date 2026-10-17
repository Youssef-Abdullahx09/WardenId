using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;
using WardenId.SharedKernel.Domain;
using WardenId.SharedKernel.Persistence;
using WardenId.SharedKernel.Persistence.Audit;

namespace WardenId.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext(
    DbContextOptions<IdentityDbContext> options) : DbContextBase(options), IIdentityDbContext
{
    public const string Schema = "Identity";

    public DbSet<User> Users => Set<User>();
    public DbSet<IdentityTenantPolicy> TenantPolicies => Set<IdentityTenantPolicy>();

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

    // A child row is kept, soft-deleted, only when it goes with its user (the cascade of the
    // user's soft delete), so that a restore brings it back. A child the user dropped (a revoked
    // MFA factor, cleared recovery codes, a replaced credential, history beyond the policy's
    // count) is deleted: children load with no IsDeleted filter, so a kept row would load again,
    // and the secrets it holds have no further use. The domain events record those changes.
    protected override bool KeepsDeletedRow(EntityEntry<Entity> entry)
    {
        if (entry.Metadata.FindProperty(SchemaConsts.UserForeignKey) is null) return true;

        var ownerId = (Guid)entry.Property(SchemaConsts.UserForeignKey).OriginalValue!;
        return ChangeTracker.Entries<User>().Any(u => u.Entity.Id == ownerId && u.Entity.IsDeleted);
    }
}
