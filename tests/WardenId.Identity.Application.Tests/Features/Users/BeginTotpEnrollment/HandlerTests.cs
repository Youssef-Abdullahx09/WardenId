using Microsoft.Extensions.Time.Testing;
using WardenId.Identity.Application.Features.Users.BeginTotpEnrollment;
using WardenId.Identity.Application.Mfa;
using Command = WardenId.Identity.Integration.Command.Features.Users.BeginTotpEnrollment.Command;

namespace WardenId.Identity.Application.Tests.Features.Users.BeginTotpEnrollment;

public class HandlerTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly FakeUserRepository _repository = new();
    private readonly FakeTotpSecretProtector _protector = new();

    private Handler CreateHandler(MfaPolicyMode mode = MfaPolicyMode.Optional) =>
        new(_repository, TestPolicies.Resolver(new IdentityPolicyOptions { MfaPolicyMode = mode }), _protector,
            new FakeTimeProvider(TestMfa.Now));

    private async Task<User> SeedUserAsync(bool enrollMfa = true)
    {
        var user = TestUsers.CreatePendingVerification(_tenantId);
        if (enrollMfa) user.AddRequiredAction(RequiredAction.EnrollMfa);
        await _repository.CreateAsync(user, user.Id);
        return user;
    }

    [Fact]
    public async Task Handle_AddsAnUnverifiedFactor_AndReturnsItsSecretOnce()
    {
        var user = await SeedUserAsync();

        var result = await CreateHandler().Handle(new Command(_tenantId, user.Id, "Acme"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var factor = Assert.Single(user.MfaFactors);
        Assert.Equal(result.Value.FactorId, factor.Id);
        Assert.False(factor.Enabled);
        Assert.Equal(TestMfa.Now, factor.EnrolledAtUtc);
        Assert.Matches("^[A-Z2-7]{32}$", result.Value.Secret);
        Assert.DoesNotContain(result.Value.Secret, factor.CredentialData);
        Assert.Equal(result.Value.Secret, Totp.ToBase32(_protector.TryUnprotect(factor.CredentialData)));
        Assert.Equal(
            $"otpauth://totp/Acme:someone%40example.com?secret={result.Value.Secret}&issuer=Acme&algorithm=SHA1&digits=6&period=30",
            result.Value.OtpAuthUri);
        Assert.Equal(1, _repository.UpdateCallCount);
    }

    [Fact]
    public async Task Handle_ReplacesAnEarlierUnfinishedEnrollment()
    {
        var user = await SeedUserAsync();
        var first = await CreateHandler().Handle(new Command(_tenantId, user.Id, "Acme"), CancellationToken.None);

        var second = await CreateHandler().Handle(new Command(_tenantId, user.Id, "Acme"), CancellationToken.None);

        Assert.Equal(second.Value.FactorId, Assert.Single(user.MfaFactors).Id);
        Assert.NotEqual(first.Value.Secret, second.Value.Secret);
    }

    [Fact]
    public async Task Handle_IsAllowed_WhenThePolicyRequiresMfa_AndTheUserHasNoFactor()
    {
        var user = await SeedUserAsync(enrollMfa: false);

        var result = await CreateHandler(MfaPolicyMode.Required).Handle(new Command(_tenantId, user.Id, "Acme"), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_Fails_WhenTheUserHasNothingToEnroll()
    {
        var user = await SeedUserAsync(enrollMfa: false);
        user.AddEnabledTotp();

        var result = await CreateHandler(MfaPolicyMode.Required).Handle(new Command(_tenantId, user.Id, "Acme"), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Single(user.MfaFactors);
        Assert.Equal(0, _repository.UpdateCallCount);
    }

    [Fact]
    public async Task Handle_Fails_ForAnotherTenantsUser()
    {
        var user = await SeedUserAsync();

        var result = await CreateHandler().Handle(new Command(Guid.NewGuid(), user.Id, "Acme"), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Empty(user.MfaFactors);
    }

    [Fact]
    public async Task Handle_Fails_WithoutAnIssuer()
    {
        var user = await SeedUserAsync();

        var result = await CreateHandler().Handle(new Command(_tenantId, user.Id, " "), CancellationToken.None);

        Assert.True(result.IsFailed);
    }
}
