namespace WardenId.Identity.Application.Tests.Fakes;

public sealed class FakeIdentityTenantPolicyRepository : IIdentityTenantPolicyRepository
{
    private readonly Dictionary<Guid, IdentityTenantPolicy> _byTenant = new();

    public int GetOverridesCallCount { get; private set; }
    public int CreateCallCount { get; private set; }
    public int UpdateCallCount { get; private set; }
    public Guid? LastActorId { get; private set; }

    /// <summary>Stores a tenant's overrides directly, as if an admin had saved them.</summary>
    public FakeIdentityTenantPolicyRepository With(Guid tenantId, IdentityPolicyOverrides overrides)
    {
        _byTenant[tenantId] = IdentityTenantPolicy.Create(tenantId, overrides).Value;
        return this;
    }

    public Task<IdentityTenantPolicy?> GetAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_byTenant.GetValueOrDefault(tenantId));

    public Task<IdentityPolicyOverrides?> GetOverridesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        GetOverridesCallCount++;
        return Task.FromResult(_byTenant.GetValueOrDefault(tenantId)?.Overrides);
    }

    public Task CreateAsync(IdentityTenantPolicy policy, Guid userId, CancellationToken cancellationToken = default)
    {
        CreateCallCount++;
        LastActorId = userId;
        _byTenant[policy.TenantId] = policy;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(IdentityTenantPolicy policy, Guid userId, CancellationToken cancellationToken = default)
    {
        UpdateCallCount++;
        LastActorId = userId;
        return Task.CompletedTask;
    }
}
