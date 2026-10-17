using WardenId.Identity.Application.Features.Policies.GetIdentity;

namespace WardenId.Identity.Application.Tests.Features.Policies.GetIdentity;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static Handler NewHandler(IdentityPolicyOptions options, FakeIdentityTenantPolicyRepository? tenantPolicies = null) =>
        new(tenantPolicies ?? new FakeIdentityTenantPolicyRepository(), Microsoft.Extensions.Options.Options.Create(options));

    [Fact]
    public async Task Handle_ReturnsConfiguredPolicy()
    {
        var options = new IdentityPolicyOptions
        {
            PasswordMinLength = 12,
            PasswordRequireUppercase = false,
            PasswordRequireDigit = true,
            PasswordRequireSymbol = true,
            PasswordBreachCheckEnabled = false,
            PasswordHistoryCount = 3,
            MaxFailedLoginAttempts = 7,
            LockoutDuration = TimeSpan.FromMinutes(30),
            MfaPolicyMode = MfaPolicyMode.Required,
            SelfRegistrationEnabled = false
        };

        var result = await NewHandler(options).Handle(new Query(TenantId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var policy = result.Value;
        Assert.Equal(new PasswordSettings(12, false, true, true, false, 3), policy.Password);
        Assert.Equal(new LockoutSettings(7, "PT30M"), policy.Lockout);
        Assert.Equal(MfaPolicyMode.Required, policy.Mfa.Mode);
        Assert.False(policy.SelfRegistration.Enabled);
        Assert.Equal("global", policy.Scope);
        Assert.Empty(policy.Overrides);
    }

    [Fact]
    public async Task Handle_ReturnsCodeDefaults_WhenNothingIsConfigured()
    {
        var result = await NewHandler(new IdentityPolicyOptions()).Handle(new Query(TenantId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var policy = result.Value;
        Assert.Equal(new PasswordSettings(8, true, true, false, true, 5), policy.Password);
        Assert.Equal(new LockoutSettings(5, "PT15M"), policy.Lockout);
        Assert.Equal(MfaPolicyMode.Optional, policy.Mfa.Mode);
        Assert.True(policy.SelfRegistration.Enabled);
        Assert.Null(policy.Terms.Url);
    }

    [Fact]
    public async Task Handle_TheTenantsTermsUrl_WinsOverTheGlobalOne()
    {
        var options = new IdentityPolicyOptions { TermsUrl = "https://global.test/terms" };
        var global = (await NewHandler(options).Handle(new Query(TenantId), CancellationToken.None)).Value;
        var tenantPolicies = new FakeIdentityTenantPolicyRepository().With(TenantId, IdentityPolicyOverrides.None with { TermsUrl = "https://tenant.test/terms" });

        var tenant = (await NewHandler(options, tenantPolicies).Handle(new Query(TenantId), CancellationToken.None)).Value;

        Assert.Equal("https://global.test/terms", global.Terms.Url);
        Assert.Empty(global.Overrides);
        Assert.Equal("https://tenant.test/terms", tenant.Terms.Url);
        Assert.Equal(["terms.url"], tenant.Overrides);
    }

    [Fact]
    public async Task Handle_ReturnsTheEffectiveValues_AndWhichComeFromTheTenant()
    {
        var tenantPolicies = new FakeIdentityTenantPolicyRepository().With(TenantId, IdentityPolicyOverrides.None with
        {
            PasswordMinLength = 14,
            LockoutDuration = TimeSpan.FromHours(1),
            MfaMode = MfaPolicyMode.Required,
            SelfRegistrationEnabled = false
        });

        var policy = (await NewHandler(new IdentityPolicyOptions(), tenantPolicies).Handle(new Query(TenantId), CancellationToken.None)).Value;

        Assert.Equal(new PasswordSettings(14, true, true, false, true, 5), policy.Password);
        Assert.Equal(new LockoutSettings(5, "PT1H"), policy.Lockout);
        Assert.Equal(MfaPolicyMode.Required, policy.Mfa.Mode);
        Assert.False(policy.SelfRegistration.Enabled);
        Assert.Equal("tenant", policy.Scope);
        Assert.Equal(
            ["password.minLength", "lockout.lockoutDuration", "mfa.mode", "selfRegistration.enabled"],
            policy.Overrides);
    }

    [Fact]
    public async Task Handle_ReportsEveryOverriddenField()
    {
        var tenantPolicies = new FakeIdentityTenantPolicyRepository().With(TenantId, new IdentityPolicyOverrides(
            12, false, false, true, false, 0, 10, TimeSpan.FromMinutes(5), MfaPolicyMode.Off, true, "https://example.test/terms"));

        var policy = (await NewHandler(new IdentityPolicyOptions(), tenantPolicies).Handle(new Query(TenantId), CancellationToken.None)).Value;

        Assert.Equal(
            [
                "password.minLength", "password.requireUppercase", "password.requireDigit", "password.requireSymbol",
                "password.breachCheckEnabled", "password.historyCount", "lockout.maxFailedAttempts",
                "lockout.lockoutDuration", "mfa.mode", "selfRegistration.enabled", "terms.url"
            ],
            policy.Overrides);
    }

    [Fact]
    public async Task Handle_IsGlobalScope_WhenTheTenantClearedEveryOverride()
    {
        var tenantPolicies = new FakeIdentityTenantPolicyRepository().With(TenantId, IdentityPolicyOverrides.None);

        var policy = (await NewHandler(new IdentityPolicyOptions(), tenantPolicies).Handle(new Query(TenantId), CancellationToken.None)).Value;

        Assert.Equal("global", policy.Scope);
        Assert.Empty(policy.Overrides);
    }
}
