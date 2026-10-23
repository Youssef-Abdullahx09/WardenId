using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WardenId.SharedKernel.Persistence;
using WardenId.SharedKernel.Persistence.Audit;
using WardenId.Tenancy.Application.Abstractions;
using WardenId.Tenancy.Domain;

namespace WardenId.Tenancy.Infrastructure.Persistence;

public sealed class TenancyDbContext(
    DbContextOptions<TenancyDbContext> options) : DbContextBase(options), ITenancyDbContext, IDataProtectionKeyContext
{
    public const string Schema = "Tenancy";

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<DataProtectionKey> DataProtectionKeys  => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        // Module configurations must be registered before base.OnModelCreating so the
        // SharedKernel IsActive/IsDeleted index conventions see this module's entity types.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TenancyDbContext).Assembly);

        // AuditEventConfiguration lives in SharedKernel.Persistence, a different assembly —
        // ApplyConfigurationsFromAssembly above only scans this module's own assembly, so it
        // needs applying explicitly. AuditDbContext owns the migration for audit.events; every
        // other module maps the same entity but excludes it from its own migrations.
        modelBuilder.ApplyConfiguration(new AuditEventConfiguration());
        modelBuilder.Entity<AuditEventRecord>().ToTable(AuditEventConfiguration.Table, AuditEventConfiguration.Schema, t => t.ExcludeFromMigrations());

        base.OnModelCreating(modelBuilder);
    }

}
