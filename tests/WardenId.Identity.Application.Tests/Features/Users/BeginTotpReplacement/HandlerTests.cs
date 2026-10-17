using Microsoft.Extensions.Time.Testing;
using WardenId.Identity.Application.Features.Users.BeginTotpReplacement;
using WardenId.Identity.Application.Mfa;
using WardenId.Identity.Integration.Command.Features.Users.VerifySecondFactor;
using Command = WardenId.Identity.Integration.Command.Features.Users.BeginTotpReplacement.Command;
using CompleteCommand = WardenId.Identity.Integration.Command.Features.Users.CompleteTotpEnrollment.Command;
using CompleteHandler = WardenId.Identity.Application.Features.Users.CompleteTotpEnrollment.Handler;

namespace WardenId.Identity.Application.Tests.Features.Users.BeginTotpReplacement;

public class HandlerTests
{
    private const string RecoveryCode = "abcde-fgh23";

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly FakeUserRepository _repository = new();
    private readonly FakeTotpSecretProtector _protector = new();
    private readonly FakeTimeProvider _time = new(TestMfa.Now);

    private Handler CreateHandler(IdentityPolicyOptions? options = null) =>
        new(_repository, TestPolicies.Resolver(options ?? new IdentityPolicyOptions()), _protector, _time);

    private async Task<(User User, MfaFactor Factor)> SeedUserAsync()
    {
        var user = TestUsers.CreatePendingVerification(_tenantId);
        var factor = user.AddEnabledTotp(recoveryCodes: [Domain.RecoveryCode.Create(RecoveryCodes.Hash(RecoveryCodes.Normalize(RecoveryCode)!))]);
        await _repository.CreateAsync(user, user.Id);
        return (user, factor);
    }

    [Fact]
    public async Task Handle_StartsANewEnrollment_AndKeepsTheCurrentFactorUntilItCompletes()
    {
        var (user, current) = await SeedUserAsync();

        var result = await CreateHandler().Handle(new Command(_tenantId, user.Id, TestMfa.CodeAt(TestMfa.Now), "Acme"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Totp.TimeStepAt(TestMfa.Now), current.LastAcceptedTimeStep);
        Assert.True(current.Enabled);
        var pending = Assert.Single(user.MfaFactors, f => f.Id == result.Value.FactorId);
        Assert.False(pending.Enabled);
        Assert.Equal(result.Value.Secret, Totp.ToBase32(_protector.TryUnprotect(pending.CredentialData)));
        Assert.StartsWith("otpauth://totp/Acme:", result.Value.OtpAuthUri);
        Assert.Equal(1, _repository.UpdateCallCount);
    }

    [Fact]
    public async Task CompletingTheReplacement_LeavesOnlyTheNewFactor()
    {
        var (user, current) = await SeedUserAsync();
        var begin = await CreateHandler().Handle(new Command(_tenantId, user.Id, TestMfa.CodeAt(TestMfa.Now), "Acme"), CancellationToken.None);
        var newSecret = _protector.TryUnprotect(user.MfaFactors.Single(f => f.Id == begin.Value.FactorId).CredentialData)!;

        var complete = await new CompleteHandler(_repository, _protector, _time).Handle(
            new CompleteCommand(_tenantId, user.Id, begin.Value.FactorId, TestMfa.CodeAt(TestMfa.Now, newSecret)), CancellationToken.None);

        Assert.True(complete.IsSuccess);
        var remaining = Assert.Single(user.MfaFactors);
        Assert.Equal(begin.Value.FactorId, remaining.Id);
        Assert.True(remaining.Enabled);
        Assert.DoesNotContain(user.MfaFactors, f => f.Id == current.Id);
        Assert.Equal(RecoveryCodes.Count, user.RemainingRecoveryCodeCount);
    }

    [Fact]
    public async Task Handle_AcceptsARecoveryCode()
    {
        var (user, _) = await SeedUserAsync();

        var result = await CreateHandler().Handle(new Command(_tenantId, user.Id, RecoveryCode, "Acme"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, user.RemainingRecoveryCodeCount);
    }

    [Fact]
    public async Task Handle_AWrongCode_IsAFailedLogin_TaggedLikeVerifySecondFactor_AndStartsNothing()
    {
        var (user, _) = await SeedUserAsync();

        var result = await CreateHandler().Handle(new Command(_tenantId, user.Id, "000000", "Acme"), CancellationToken.None);

        Assert.Equal(SecondFactorFailureReason.InvalidCode, SecondFactorFailure.Read(result.Errors));
        Assert.Equal(UserErrors.MfaCodeInvalid().Message, result.Errors[0].Message);
        Assert.Equal(1, user.Lockout.FailedAttemptCount);
        Assert.Single(user.MfaFactors);
    }

    [Fact]
    public async Task Handle_RefusesAReplayedCode()
    {
        var (user, _) = await SeedUserAsync();
        var code = TestMfa.CodeAt(TestMfa.Now);
        await CreateHandler().Handle(new Command(_tenantId, user.Id, code, "Acme"), CancellationToken.None);

        var replay = await CreateHandler().Handle(new Command(_tenantId, user.Id, code, "Acme"), CancellationToken.None);

        Assert.Equal(SecondFactorFailureReason.InvalidCode, SecondFactorFailure.Read(replay.Errors));
    }

    [Fact]
    public async Task Handle_IsNotEnrolled_WithoutAnEnabledFactor()
    {
        var user = TestUsers.CreatePendingVerification(_tenantId);
        await _repository.CreateAsync(user, user.Id);

        var result = await CreateHandler().Handle(new Command(_tenantId, user.Id, "123456", "Acme"), CancellationToken.None);

        Assert.Equal(SecondFactorFailureReason.NotEnrolled, SecondFactorFailure.Read(result.Errors));
        Assert.Empty(user.MfaFactors);
    }

    [Fact]
    public async Task Handle_Fails_WithoutAnIssuer_BeforeCheckingTheCode()
    {
        var (user, _) = await SeedUserAsync();

        var result = await CreateHandler().Handle(new Command(_tenantId, user.Id, "000000", " "), CancellationToken.None);

        Assert.Equal(UserErrors.TotpIssuerRequired().Message, result.Errors[0].Message);
        Assert.Equal(0, user.Lockout.FailedAttemptCount);
    }

    [Fact]
    public async Task Handle_AWrongCodeThatLocksTheAccount_IsTaggedLockedOut()
    {
        var (user, _) = await SeedUserAsync();

        var result = await CreateHandler(new IdentityPolicyOptions { MaxFailedLoginAttempts = 1 })
            .Handle(new Command(_tenantId, user.Id, "000000", "Acme"), CancellationToken.None);

        Assert.Equal(SecondFactorFailureReason.LockedOut, SecondFactorFailure.Read(result.Errors));
        Assert.Equal(UserErrorCodes.AccountLocked, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
    }

    [Theory]
    [InlineData("locked")]
    [InlineData("disabled")]
    public async Task Handle_AnInactiveAccount_IsTaggedAccountInactive(string state)
    {
        var (user, _) = await SeedUserAsync();
        if (state == "locked") user.RecordFailedLogin(1, TimeSpan.FromHours(1));
        else user.Disable("reason");

        var result = await CreateHandler().Handle(new Command(_tenantId, user.Id, TestMfa.CodeAt(TestMfa.Now), "Acme"), CancellationToken.None);

        Assert.Equal(SecondFactorFailureReason.AccountInactive, SecondFactorFailure.Read(result.Errors));
        Assert.Single(user.MfaFactors);
    }
}
