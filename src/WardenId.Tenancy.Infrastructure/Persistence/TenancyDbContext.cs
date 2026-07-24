using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WardenId.SharedKernel.Persistence;
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

        base.OnModelCreating(modelBuilder);
    }

}
