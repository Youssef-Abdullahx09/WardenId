using Microsoft.Extensions.Time.Testing;
using WardenId.Identity.Application.Features.Users.RemoveMfa;
using WardenId.Identity.Application.Mfa;
using WardenId.Identity.Integration.Command.Features.Users.VerifySecondFactor;
using Command = WardenId.Identity.Integration.Command.Features.Users.RemoveMfa.Command;

namespace WardenId.Identity.Application.Tests.Features.Users.RemoveMfa;

public class HandlerTests
{
    private const string RecoveryCode = "abcde-fgh23";

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly FakeUserRepository _repository = new();

    private Handler CreateHandler(MfaPolicyMode mode = MfaPolicyMode.Optional) =>
        new(_repository, TestPolicies.Resolver(new IdentityPolicyOptions { MfaPolicyMode = mode }), new FakeTotpSecretProtector(),
            new FakeTimeProvider(TestMfa.Now));

    private async Task<User> SeedUserAsync()
    {
        var user = TestUsers.CreatePendingVerification(_tenantId);
        user.AddEnabledTotp(recoveryCodes: [Domain.RecoveryCode.Create(RecoveryCodes.Hash(RecoveryCodes.Normalize(RecoveryCode)!))]);
        await _repository.CreateAsync(user, user.Id);
        return user;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handle_RemovesEveryFactorAndRecoveryCode_WithATotpOrRecoveryCode(bool useRecoveryCode)
    {
        var user = await SeedUserAsync();
        var code = useRecoveryCode ? RecoveryCode : TestMfa.CodeAt(TestMfa.Now);

        var result = await CreateHandler().Handle(new Command(_tenantId, user.Id, code), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(user.MfaFactors);
        Assert.Empty(user.RecoveryCodes);
        Assert.DoesNotContain(RequiredAction.EnrollMfa, user.PendingActions);
        Assert.Equal(1, _repository.UpdateCallCount);
    }

    [Theory]
    [InlineData(MfaPolicyMode.Required)]
    [InlineData(MfaPolicyMode.RequiredForAdmins)]
    public async Task Handle_IsRefused_WhileThePolicyRequiresMfa_WithoutCheckingTheCode(MfaPolicyMode mode)
    {
        var user = await SeedUserAsync();

        var result = await CreateHandler(mode).Handle(new Command(_tenantId, user.Id, "000000"), CancellationToken.None);

        Assert.Equal(UserErrors.MfaRequiredCannotRemove().Message, Assert.Single(result.Errors).Message);
        Assert.Null(SecondFactorFailure.Read(result.Errors));
        Assert.Single(user.MfaFactors);
        Assert.Equal(0, user.Lockout.FailedAttemptCount);
        Assert.Equal(0, _repository.UpdateCallCount);
    }

    [Fact]
    public async Task Handle_AWrongCode_IsAFailedLogin_AndRemovesNothing()
    {
        var user = await SeedUserAsync();

        var result = await CreateHandler().Handle(new Command(_tenantId, user.Id, "000000"), CancellationToken.None);

        Assert.Equal(SecondFactorFailureReason.InvalidCode, SecondFactorFailure.Read(result.Errors));
        Assert.Equal(1, user.Lockout.FailedAttemptCount);
        Assert.Single(user.MfaFactors);
        Assert.Single(user.RecoveryCodes);
    }

    [Fact]
    public async Task Handle_AWrongCodeThatLocksTheAccount_IsTaggedLockedOut()
    {
        var user = await SeedUserAsync();
        var handler = new Handler(_repository, TestPolicies.Resolver(new IdentityPolicyOptions { MaxFailedLoginAttempts = 1 }),
            new FakeTotpSecretProtector(), new FakeTimeProvider(TestMfa.Now));

        var result = await handler.Handle(new Command(_tenantId, user.Id, "000000"), CancellationToken.None);

        Assert.Equal(SecondFactorFailureReason.LockedOut, SecondFactorFailure.Read(result.Errors));
        Assert.Equal(UserStatus.Locked, user.Status);
        Assert.Single(user.MfaFactors);
    }

    [Theory]
    [InlineData("locked")]
    [InlineData("disabled")]
    public async Task Handle_AnInactiveAccount_IsTaggedAccountInactive(string state)
    {
        var user = await SeedUserAsync();
        if (state == "locked") user.RecordFailedLogin(1, TimeSpan.FromHours(1));
        else user.Disable("reason");

        var result = await CreateHandler().Handle(new Command(_tenantId, user.Id, TestMfa.CodeAt(TestMfa.Now)), CancellationToken.None);

        Assert.Equal(SecondFactorFailureReason.AccountInactive, SecondFactorFailure.Read(result.Errors));
        Assert.Single(user.MfaFactors);
    }
}
