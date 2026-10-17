using WardenId.Identity.Application.Features.Users.GetAccount;
using WardenId.Identity.Integration.Command.Features.Users.GetAccount;

namespace WardenId.Identity.Application.Tests.Features.Users.GetAccount;

public class HandlerTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly FakeUserRepository _repository = new();

    private Handler CreateHandler(IdentityPolicyOptions? options = null, FakeIdentityTenantPolicyRepository? tenantPolicies = null) =>
        new(_repository, TestPolicies.Resolver(options ?? new IdentityPolicyOptions(), tenantPolicies));

    [Fact]
    public async Task Handle_ReturnsTheAccount_WithOnlyEnabledFactors()
    {
        var user = TestUsers.CreatePendingVerification(_tenantId);
        var enabled = user.AddEnabledTotp(recoveryCodes: [RecoveryCode.Create("a"), RecoveryCode.Create("b")]);
        user.BeginMfaEnrollment(MfaFactor.Create(MfaFactorType.Totp, "pending", TestMfa.Now));
        user.AcceptTerms(TestMfa.Now);
        await _repository.CreateAsync(user, user.Id);

        var result = await CreateHandler().Handle(new Query(_tenantId, user.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var account = result.Value;
        Assert.Equal("someone@example.com", account.Email);
        Assert.Equal("someone", account.Username);
        Assert.Equal(TestMfa.Now, account.TermsAcceptedAtUtc);
        Assert.Null(account.TermsUrl);
        Assert.True(account.MfaAvailable);
        Assert.False(account.MfaRequired);
        Assert.Equal(new AccountFactor(enabled.Id, "Totp", enabled.EnrolledAtUtc, enabled.LastUsedAtUtc), Assert.Single(account.Factors));
        Assert.Equal(2, account.RemainingRecoveryCodes);
    }

    [Theory]
    [InlineData(MfaPolicyMode.Off, false, false)]
    [InlineData(MfaPolicyMode.Optional, true, false)]
    [InlineData(MfaPolicyMode.Required, true, true)]
    [InlineData(MfaPolicyMode.RequiredForAdmins, true, true)]
    public async Task Handle_ReportsWhatTheMfaPolicyAllows(MfaPolicyMode mode, bool available, bool required)
    {
        var user = TestUsers.CreatePendingVerification(_tenantId);
        await _repository.CreateAsync(user, user.Id);

        var account = (await CreateHandler(new IdentityPolicyOptions { MfaPolicyMode = mode })
            .Handle(new Query(_tenantId, user.Id), CancellationToken.None)).Value;

        Assert.Equal(available, account.MfaAvailable);
        Assert.Equal(required, account.MfaRequired);
        Assert.Empty(account.Factors);
    }

    [Fact]
    public async Task Handle_TheTermsUrl_IsTheTenantsOverTheGlobalOne()
    {
        var user = TestUsers.CreatePendingVerification(_tenantId);
        await _repository.CreateAsync(user, user.Id);
        var options = new IdentityPolicyOptions { TermsUrl = "https://global.test/terms" };
        var tenantPolicies = new FakeIdentityTenantPolicyRepository()
            .With(_tenantId, IdentityPolicyOverrides.None with { TermsUrl = "https://tenant.test/terms" });

        var global = (await CreateHandler(options).Handle(new Query(_tenantId, user.Id), CancellationToken.None)).Value;
        var tenant = (await CreateHandler(options, tenantPolicies).Handle(new Query(_tenantId, user.Id), CancellationToken.None)).Value;

        Assert.Equal("https://global.test/terms", global.TermsUrl);
        Assert.Equal("https://tenant.test/terms", tenant.TermsUrl);
    }

    [Fact]
    public async Task Handle_Fails_ForAnotherTenantsUser()
    {
        var user = TestUsers.CreatePendingVerification(_tenantId);
        await _repository.CreateAsync(user, user.Id);

        var result = await CreateHandler().Handle(new Query(Guid.NewGuid(), user.Id), CancellationToken.None);

        Assert.True(result.IsFailed);
    }
}
