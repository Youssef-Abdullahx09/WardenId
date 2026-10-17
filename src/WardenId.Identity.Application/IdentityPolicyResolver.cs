using FluentResults;
using Microsoft.Extensions.Options;
using WardenId.Identity.Application.Abstractions;

namespace WardenId.Identity.Application;

/// <summary>Resolves the identity policy in force for a tenant: its stored overrides over the global options, cached briefly.</summary>
public sealed class IdentityPolicyResolver(
    IIdentityTenantPolicyRepository tenantPolicies,
    IdentityTenantPolicyCache cache,
    IOptions<IdentityPolicyOptions> options)
{
    public async Task<Result<EffectiveIdentityPolicy>> ResolveAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!cache.TryGet(tenantId, out var overrides))
        {
            var generation = cache.Generation(tenantId);
            overrides = await tenantPolicies.GetOverridesAsync(tenantId, cancellationToken);
            cache.Set(tenantId, overrides, generation);
        }

        return EffectiveIdentityPolicy.Merge(options.Value, overrides);
    }
}
