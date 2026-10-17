using Microsoft.EntityFrameworkCore;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Infrastructure.Persistence;

public sealed class IdentityTenantPolicyRepository(IIdentityDbContext dbContext) : IIdentityTenantPolicyRepository
{
    public Task<IdentityTenantPolicy?> GetAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        dbContext.TenantPolicies.FirstOrDefaultAsync(p => p.TenantId == tenantId, cancellationToken);

    // Untracked: sign-in reads this and then saves the user, which mustn't drag the policy into that save.
    public async Task<IdentityPolicyOverrides?> GetOverridesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var policy = await dbContext.TenantPolicies
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId, cancellationToken);

        return policy?.Overrides;
    }

    public async Task CreateAsync(IdentityTenantPolicy policy, Guid userId, CancellationToken cancellationToken = default)
    {
        await dbContext.TenantPolicies.AddAsync(policy, cancellationToken);
        await dbContext.SaveChangesAsync(userId, cancellationToken);
    }

    public Task UpdateAsync(IdentityTenantPolicy policy, Guid userId, CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(userId, cancellationToken);
}
