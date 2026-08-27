using Microsoft.EntityFrameworkCore;
using WardenId.ClientManagement.Application.Abstractions;
using WardenId.ClientManagement.Domain;
using WardenId.SharedKernel.Persistence;
using WardenId.SharedKernel.Persistence.Audit;

namespace WardenId.ClientManagement.Infrastructure.Persistence;

public sealed class ClientManagementDbContext(
    DbContextOptions<ClientManagementDbContext> options) : DbContextBase(options), IClientManagementDbContext
{
    public const string Schema = "ClientManagement";

    public DbSet<Client> Clients => Set<Client>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ClientManagementDbContext).Assembly);

        // AuditEventConfiguration lives in SharedKernel.Persistence, a different assembly —
        // ApplyConfigurationsFromAssembly above only scans this module's own assembly, so it
        // needs applying explicitly. AuditDbContext owns the migration for audit.events; every
        // other module maps the same entity but excludes it from its own migrations.
        modelBuilder.ApplyConfiguration(new AuditEventConfiguration());
        modelBuilder.Entity<AuditEventRecord>().ToTable("events", "audit", t => t.ExcludeFromMigrations());

        base.OnModelCreating(modelBuilder);
    }
}
