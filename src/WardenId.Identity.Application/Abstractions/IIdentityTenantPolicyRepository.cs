using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Abstractions;

public interface IIdentityTenantPolicyRepository
{
    /// <summary>The tenant's policy, tracked for an update; null when the tenant never had overrides.</summary>
    Task<IdentityTenantPolicy?> GetAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>A read-only copy of the tenant's overrides; null when it has none stored.</summary>
    Task<IdentityPolicyOverrides?> GetOverridesAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task CreateAsync(IdentityTenantPolicy policy, Guid userId, CancellationToken cancellationToken = default);

    Task UpdateAsync(IdentityTenantPolicy policy, Guid userId, CancellationToken cancellationToken = default);
}
