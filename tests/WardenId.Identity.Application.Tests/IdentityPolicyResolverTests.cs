using Microsoft.Extensions.Time.Testing;

namespace WardenId.Identity.Application.Tests;

public class IdentityPolicyResolverTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static readonly IdentityPolicyOptions Global = new()
    {
        PasswordMinLength = 10,
        PasswordRequireUppercase = true,
        PasswordRequireDigit = true,
        PasswordRequireSymbol = false,
        PasswordBreachCheckEnabled = true,
        PasswordHistoryCount = 5,
        MaxFailedLoginAttempts = 5,
        LockoutDuration = TimeSpan.FromMinutes(15),
        MfaPolicyMode = MfaPolicyMode.Optional,
        SelfRegistrationEnabled = true,
        CurrentPasswordAlgorithmVersion = 3
    };

    [Fact]
    public async Task Resolve_ReturnsTheGlobalValues_WhenTheTenantHasNoOverrides()
    {
        var result = await TestPolicies.Resolver(Global).ResolveAsync(TenantId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var policy = result.Value;
        Assert.Equal(PasswordPolicy.FromTrustedSource(10, true, true, false, true, 5), policy.Password);
        Assert.Equal(5, policy.MaxFailedLoginAttempts);
        Assert.Equal(TimeSpan.FromMinutes(15), policy.LockoutDuration);
        Assert.Equal(MfaPolicyMode.Optional, policy.Mfa.Mode);
        Assert.True(policy.SelfRegistrationEnabled);
        Assert.Equal(3, policy.CurrentPasswordAlgorithmVersion);
        Assert.False(policy.Overrides.HasAny);
    }

    [Fact]
    public async Task Resolve_LayersEachTenantOverride_OverTheGlobalValue()
    {
        var tenantPolicies = new FakeIdentityTenantPolicyRepository().With(TenantId, IdentityPolicyOverrides.None with
        {
            PasswordMinLength = 14,
            PasswordRequireSymbol = true,
            MaxFailedLoginAttempts = 3,
            MfaMode = MfaPolicyMode.Required,
            SelfRegistrationEnabled = false
        });

        var policy = (await TestPolicies.Resolver(Global, tenantPolicies).ResolveAsync(TenantId, CancellationToken.None)).Value;

        Assert.Equal(PasswordPolicy.FromTrustedSource(14, true, true, true, true, 5), policy.Password);
        Assert.Equal(3, policy.MaxFailedLoginAttempts);
        Assert.Equal(TimeSpan.FromMinutes(15), policy.LockoutDuration);
        Assert.Equal(MfaPolicyMode.Required, policy.Mfa.Mode);
        Assert.False(policy.SelfRegistrationEnabled);
        Assert.Equal(3, policy.CurrentPasswordAlgorithmVersion);
    }

    [Fact]
    public async Task Resolve_DoesNotApplyOneTenantsOverrides_ToAnother()
    {
        var tenantPolicies = new FakeIdentityTenantPolicyRepository()
            .With(TenantId, IdentityPolicyOverrides.None with { MaxFailedLoginAttempts = 3 });

        var policy = (await TestPolicies.Resolver(Global, tenantPolicies).ResolveAsync(Guid.NewGuid(), CancellationToken.None)).Value;

        Assert.Equal(5, policy.MaxFailedLoginAttempts);
    }

    [Fact]
    public async Task Resolve_CachesPerTenant_IncludingTenantsWithoutOverrides()
    {
        var tenantPolicies = new FakeIdentityTenantPolicyRepository();
        var resolver = TestPolicies.Resolver(Global, tenantPolicies);

        await resolver.ResolveAsync(TenantId, CancellationToken.None);
        await resolver.ResolveAsync(TenantId, CancellationToken.None);

        Assert.Equal(1, tenantPolicies.GetOverridesCallCount);
    }

    [Fact]
    public async Task Resolve_ReadsAgain_AfterTheTenantIsInvalidated()
    {
        var tenantPolicies = new FakeIdentityTenantPolicyRepository();
        var cache = new IdentityTenantPolicyCache(TimeProvider.System);
        var resolver = TestPolicies.Resolver(Global, tenantPolicies, cache);
        await resolver.ResolveAsync(TenantId, CancellationToken.None);

        tenantPolicies.With(TenantId, IdentityPolicyOverrides.None with { MaxFailedLoginAttempts = 2 });
        cache.Invalidate(TenantId);
        var policy = (await resolver.ResolveAsync(TenantId, CancellationToken.None)).Value;

        Assert.Equal(2, policy.MaxFailedLoginAttempts);
        Assert.Equal(2, tenantPolicies.GetOverridesCallCount);
    }

    [Fact]
    public async Task Resolve_ReadsAgain_OnceTheCachedEntryExpires()
    {
        var time = new FakeTimeProvider();
        var tenantPolicies = new FakeIdentityTenantPolicyRepository();
        var resolver = TestPolicies.Resolver(Global, tenantPolicies, new IdentityTenantPolicyCache(time));
        await resolver.ResolveAsync(TenantId, CancellationToken.None);

        time.Advance(IdentityTenantPolicyCache.Lifetime - TimeSpan.FromSeconds(1));
        await resolver.ResolveAsync(TenantId, CancellationToken.None);
        Assert.Equal(1, tenantPolicies.GetOverridesCallCount);

        time.Advance(TimeSpan.FromSeconds(2));
        await resolver.ResolveAsync(TenantId, CancellationToken.None);
        Assert.Equal(2, tenantPolicies.GetOverridesCallCount);
    }

    [Fact]
    public void Cache_IgnoresAValueReadBeforeAnInvalidation()
    {
        var cache = new IdentityTenantPolicyCache(TimeProvider.System);
        var generation = cache.Generation(TenantId);

        cache.Invalidate(TenantId);
        cache.Set(TenantId, IdentityPolicyOverrides.None with { MaxFailedLoginAttempts = 2 }, generation);

        Assert.False(cache.TryGet(TenantId, out _));
    }
}
