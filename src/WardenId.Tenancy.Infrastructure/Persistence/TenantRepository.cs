using FluentResults;
using Microsoft.EntityFrameworkCore;
using WardenId.Tenancy.Application.Abstractions;
using WardenId.Tenancy.Domain;

namespace WardenId.Tenancy.Infrastructure.Persistence;

public sealed class TenantRepository(
    ITenancyDbContext dbContext) : ITenantRepository
{
    public async Task CreateAsync(
        Tenant tenant, 
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Tenants.AddAsync(tenant, cancellationToken);
        await dbContext.SaveChangesAsync(userId, cancellationToken);
    }

    public async Task<Result<Tenant>> GetByIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants.Include(t => t.SigningKeys).Where(x => x.Id == tenantId
                                                        && !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        return tenant is null ? Result.Fail(TenantErrors.NotFound) : tenant;
    }

    public async Task UpdateAsync(
        Tenant tenant,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(userId, cancellationToken);
    }

    public async Task<Result<Tenant>> GetBySlugAsync(
        Slug slug,
        CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants.Include(t => t.SigningKeys).Where(x => x.Slug == slug
                                                        && !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
        
        return tenant is null  ? Result.Fail(TenantErrors.NotFound) : tenant;
    }

    public async Task<bool> ExistsBySlugAsync(
        Slug slug,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Tenants.Where(x => x.Slug == slug
                                                  && !x.IsDeleted)
            .AnyAsync(cancellationToken);
    }
}