using Microsoft.EntityFrameworkCore;

namespace WardenId.Tenancy.Application.Tests.Fakes;

/// <summary>Same queries as the real TenantRepository, over the InMemory test context.</summary>
public sealed class FakeTenantRepository(TestTenancyDbContext db) : ITenantRepository
{
    public int UpdateCallCount { get; private set; }
    public Guid? LastActorId { get; private set; }

    public async Task CreateAsync(Tenant tenant, Guid userId, CancellationToken cancellationToken = default)
    {
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Result<Tenant>> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await db.Tenants.Include(t => t.SigningKeys)
            .FirstOrDefaultAsync(t => t.Id == tenantId && !t.IsDeleted, cancellationToken);
        return tenant is null ? Result.Fail(TenantErrors.NotFound) : tenant;
    }

    public async Task UpdateAsync(Tenant tenant, Guid userId, CancellationToken cancellationToken = default)
    {
        UpdateCallCount++;
        LastActorId = userId;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Result<Tenant>> GetBySlugAsync(Slug slug, CancellationToken cancellationToken = default)
    {
        var tenant = await db.Tenants.Include(t => t.SigningKeys)
            .FirstOrDefaultAsync(t => t.Slug == slug && !t.IsDeleted, cancellationToken);
        return tenant is null ? Result.Fail(TenantErrors.NotFound) : tenant;
    }

    public Task<bool> ExistsBySlugAsync(Slug slug, CancellationToken cancellationToken = default) =>
        db.Tenants.AnyAsync(t => t.Slug == slug && !t.IsDeleted, cancellationToken);
}
