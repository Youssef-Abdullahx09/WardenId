using Microsoft.Extensions.Options;

namespace WardenId.Identity.Application.Tests.Fakes;

public static class TestPolicies
{
    /// <summary>A resolver over the given global options and, when given, stored tenant overrides.</summary>
    public static IdentityPolicyResolver Resolver(
        IdentityPolicyOptions? global = null,
        FakeIdentityTenantPolicyRepository? tenantPolicies = null,
        IdentityTenantPolicyCache? cache = null) =>
        new(
            tenantPolicies ?? new FakeIdentityTenantPolicyRepository(),
            cache ?? new IdentityTenantPolicyCache(TimeProvider.System),
            Options.Create(global ?? new IdentityPolicyOptions()));
}
