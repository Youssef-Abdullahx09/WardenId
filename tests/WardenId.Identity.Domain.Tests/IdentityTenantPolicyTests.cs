using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain.Tests;

public class IdentityTenantPolicyTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static readonly IdentityPolicyOverrides Full = new(
        PasswordMinLength: 12,
        PasswordRequireUppercase: false,
        PasswordRequireDigit: true,
        PasswordRequireSymbol: true,
        PasswordBreachCheckEnabled: false,
        PasswordHistoryCount: 10,
        MaxFailedLoginAttempts: 3,
        LockoutDuration: TimeSpan.FromMinutes(30),
        MfaMode: MfaPolicyMode.Required,
        SelfRegistrationEnabled: false);

    [Fact]
    public void Create_StoresTheOverrides_AndRaisesChanged()
    {
        var result = IdentityTenantPolicy.Create(TenantId, Full);

        Assert.True(result.IsSuccess);
        Assert.Equal(TenantId, result.Value.TenantId);
        Assert.Equal(Full, result.Value.Overrides);
        Assert.IsType<IdentityTenantPolicyChangedDomainEvent>(Assert.Single(result.Value.DomainEvents));
    }

    [Fact]
    public void Create_AcceptsNoOverridesAtAll()
    {
        var result = IdentityTenantPolicy.Create(TenantId, IdentityPolicyOverrides.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Overrides.HasAny);
    }

    [Fact]
    public void Create_Fails_ForAnEmptyTenantId()
    {
        Assert.True(IdentityTenantPolicy.Create(Guid.Empty, Full).IsFailed);
    }

    [Fact]
    public void Replace_SetsEveryField_NullsClearingOverrides()
    {
        var policy = IdentityTenantPolicy.Create(TenantId, Full).Value;
        policy.ClearDomainEvents();

        var replacement = IdentityPolicyOverrides.None with { MaxFailedLoginAttempts = 10 };
        var result = policy.Replace(replacement);

        Assert.True(result.IsSuccess);
        Assert.Equal(replacement, policy.Overrides);
        Assert.Null(policy.PasswordMinLength);
        Assert.Single(policy.DomainEvents);
    }

    [Fact]
    public void Replace_RaisesNothing_WhenTheOverridesAreUnchanged()
    {
        var policy = IdentityTenantPolicy.Create(TenantId, Full).Value;
        policy.ClearDomainEvents();

        Assert.True(policy.Replace(Full).IsSuccess);
        Assert.Empty(policy.DomainEvents);
    }

    [Fact]
    public void Replace_LeavesThePolicyUnchanged_WhenAValueIsInvalid()
    {
        var policy = IdentityTenantPolicy.Create(TenantId, Full).Value;

        var result = policy.Replace(Full with { PasswordMinLength = 4 });

        Assert.True(result.IsFailed);
        Assert.Equal(Full, policy.Overrides);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(129)]
    public void MinLength_OutsideBounds_IsRejected(int minLength) =>
        AssertInvalid(IdentityPolicyOverrides.None with { PasswordMinLength = minLength });

    [Theory]
    [InlineData(8)]
    [InlineData(128)]
    public void MinLength_AtTheBounds_IsAccepted(int minLength) =>
        AssertValid(IdentityPolicyOverrides.None with { PasswordMinLength = minLength });

    [Theory]
    [InlineData(-1)]
    [InlineData(25)]
    public void HistoryCount_OutsideBounds_IsRejected(int historyCount) =>
        AssertInvalid(IdentityPolicyOverrides.None with { PasswordHistoryCount = historyCount });

    [Theory]
    [InlineData(0)]
    [InlineData(24)]
    public void HistoryCount_AtTheBounds_IsAccepted(int historyCount) =>
        AssertValid(IdentityPolicyOverrides.None with { PasswordHistoryCount = historyCount });

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void MaxFailedAttempts_OutsideBounds_IsRejected(int attempts) =>
        AssertInvalid(IdentityPolicyOverrides.None with { MaxFailedLoginAttempts = attempts });

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void MaxFailedAttempts_AtTheBounds_IsAccepted(int attempts) =>
        AssertValid(IdentityPolicyOverrides.None with { MaxFailedLoginAttempts = attempts });

    [Theory]
    [InlineData(59)]
    [InlineData(86401)]
    public void LockoutDuration_OutsideBounds_IsRejected(int seconds) =>
        AssertInvalid(IdentityPolicyOverrides.None with { LockoutDuration = TimeSpan.FromSeconds(seconds) });

    [Theory]
    [InlineData(60)]
    [InlineData(86400)]
    public void LockoutDuration_AtTheBounds_IsAccepted(int seconds) =>
        AssertValid(IdentityPolicyOverrides.None with { LockoutDuration = TimeSpan.FromSeconds(seconds) });

    [Fact]
    public void MfaMode_ThatIsNotAMember_IsRejected() =>
        AssertInvalid(IdentityPolicyOverrides.None with { MfaMode = (MfaPolicyMode)99 });

    [Fact]
    public void Audit_IsAdminConfig_TargetedByTenantId()
    {
        var policy = IdentityTenantPolicy.Create(TenantId, Full).Value;
        IAuditableAggregate auditable = policy;

        Assert.Equal("identity_policy", auditable.AuditTargetTypeKey);
        Assert.Equal(AuditableChannel.AdminConfig, auditable.AuditChannel);
        Assert.Equal(TenantId.ToString(), auditable.AuditTargetId);
    }

    private static void AssertInvalid(IdentityPolicyOverrides overrides)
    {
        var result = IdentityTenantPolicy.Create(TenantId, overrides);
        Assert.True(result.IsFailed);
        Assert.Equal(UserErrorCodes.Validation, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
    }

    private static void AssertValid(IdentityPolicyOverrides overrides) =>
        Assert.True(IdentityTenantPolicy.Create(TenantId, overrides).IsSuccess);
}
