using Microsoft.Extensions.Options;
using WardenId.Identity.Application;
using WardenId.Identity.Application.Abstractions;

namespace WardenId.Identity.Infrastructure.Tests;

/// <summary>A policy resolver with no tenant overrides, for tests about something else.</summary>
public static class GlobalPolicies
{
    public static IdentityPolicyResolver Resolver(IdentityPolicyOptions options) =>
        new(new NoTenantPolicies(), new IdentityTenantPolicyCache(TimeProvider.System), Options.Create(options));

    private sealed class NoTenantPolicies : IIdentityTenantPolicyRepository
    {
        public Task<IdentityTenantPolicy?> GetAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IdentityTenantPolicy?>(null);

        public Task<IdentityPolicyOverrides?> GetOverridesAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IdentityPolicyOverrides?>(null);

        public Task CreateAsync(IdentityTenantPolicy policy, Guid userId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task UpdateAsync(IdentityTenantPolicy policy, Guid userId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
