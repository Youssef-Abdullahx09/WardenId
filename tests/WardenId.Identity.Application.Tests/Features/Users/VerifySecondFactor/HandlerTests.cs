using Microsoft.Extensions.Time.Testing;
using WardenId.Identity.Application.Features.Users.VerifySecondFactor;
using WardenId.Identity.Application.Mfa;
using WardenId.Identity.Integration.Command.Features.Users.VerifySecondFactor;
using Command = WardenId.Identity.Integration.Command.Features.Users.VerifySecondFactor.Command;

namespace WardenId.Identity.Application.Tests.Features.Users.VerifySecondFactor;

public class HandlerTests
{
    private const string RecoveryCode = "abcde-fgh23";

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly FakeUserRepository _repository = new();
    private readonly FakeTimeProvider _time = new(TestMfa.Now);

    private Handler CreateHandler(IdentityPolicyOptions? options = null) =>
        new(_repository, TestPolicies.Resolver(options ?? new IdentityPolicyOptions()), new FakeTotpSecretProtector(), _time);

    private async Task<(User User, MfaFactor Factor)> SeedUserAsync()
    {
        var user = TestUsers.CreatePendingVerification(_tenantId);
        var codes = new[] { Domain.RecoveryCode.Create(RecoveryCodes.Hash(RecoveryCodes.Normalize(RecoveryCode)!)), Domain.RecoveryCode.Create("other") };
        var factor = user.AddEnabledTotp(recoveryCodes: codes);
        await _repository.CreateAsync(user, user.Id);
        return (user, factor);
    }

    private static SecondFactorFailureReason? FailureOf(IResultBase result)
    {
        Assert.True(result.IsFailed);
        return SecondFactorFailure.Read(result.Errors);
    }

    [Fact]
    public async Task Handle_AcceptsTheCurrentCode_AndRecordsIt()
    {
        var (user, factor) = await SeedUserAsync();

        var result = await CreateHandler().Handle(new Command(_tenantId, user.Id, TestMfa.CodeAt(TestMfa.Now)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new SecondFactorVerified(SecondFactorMethod.Totp, 2), result.Value);
        Assert.Equal(Totp.TimeStepAt(TestMfa.Now), factor.LastAcceptedTimeStep);
        Assert.Equal(TestMfa.Now, factor.LastUsedAtUtc);
    }

    [Fact]
    public async Task Handle_RefusesTheSameCodeTwice()
    {
        var (user, _) = await SeedUserAsync();
        var code = TestMfa.CodeAt(TestMfa.Now);
        await CreateHandler().Handle(new Command(_tenantId, user.Id, code), CancellationToken.None);

        var replay = await CreateHandler().Handle(new Command(_tenantId, user.Id, code), CancellationToken.None);

        Assert.Equal(SecondFactorFailureReason.InvalidCode, FailureOf(replay));
        Assert.Equal(1, user.Lockout.FailedAttemptCount);
    }

    [Fact]
    public async Task Handle_RefusesACodeOlderThanTheLastAcceptedOne()
    {
        var (user, _) = await SeedUserAsync();
        await CreateHandler().Handle(new Command(_tenantId, user.Id, TestMfa.CodeAt(TestMfa.Now.AddSeconds(30))), CancellationToken.None);

        var older = await CreateHandler().Handle(new Command(_tenantId, user.Id, TestMfa.CodeAt(TestMfa.Now)), CancellationToken.None);

        Assert.Equal(SecondFactorFailureReason.InvalidCode, FailureOf(older));
    }

    [Fact]
    public async Task Handle_AcceptsARecoveryCodeOnce_InAnyCase()
    {
        var (user, _) = await SeedUserAsync();

        var result = await CreateHandler().Handle(new Command(_tenantId, user.Id, " ABCDE FGH23 "), CancellationToken.None);
        var again = await CreateHandler().Handle(new Command(_tenantId, user.Id, RecoveryCode), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new SecondFactorVerified(SecondFactorMethod.RecoveryCode, 1), result.Value);
        Assert.Equal(SecondFactorFailureReason.InvalidCode, FailureOf(again));
    }

    [Fact]
    public async Task Handle_AWrongCode_CountsAsAFailedLogin()
    {
        var (user, _) = await SeedUserAsync();

        var result = await CreateHandler().Handle(new Command(_tenantId, user.Id, TestMfa.CodeAt(TestMfa.Now.AddMinutes(10))), CancellationToken.None);

        Assert.Equal(SecondFactorFailureReason.InvalidCode, FailureOf(result));
        Assert.Equal(UserErrorCodes.Validation, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
        Assert.Equal(1, user.Lockout.FailedAttemptCount);
        Assert.Equal(1, _repository.UpdateCallCount);
    }

    [Fact]
    public async Task Handle_LocksTheAccount_OnTheTenantsMaxFailedAttempts()
    {
        var (user, _) = await SeedUserAsync();
        var handler = CreateHandler(new IdentityPolicyOptions { MaxFailedLoginAttempts = 2 });
        await handler.Handle(new Command(_tenantId, user.Id, "000000"), CancellationToken.None);

        var result = await handler.Handle(new Command(_tenantId, user.Id, "wrong-code"), CancellationToken.None);

        Assert.Equal(SecondFactorFailureReason.LockedOut, FailureOf(result));
        Assert.Equal(UserErrorCodes.AccountLocked, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
        Assert.Equal(UserStatus.Locked, user.Status);
    }

    [Fact]
    public async Task Handle_RefusesEvenTheRightCode_WhileLocked()
    {
        var (user, factor) = await SeedUserAsync();
        user.RecordFailedLogin(1, TimeSpan.FromHours(1));

        var result = await CreateHandler().Handle(new Command(_tenantId, user.Id, TestMfa.CodeAt(TestMfa.Now)), CancellationToken.None);

        Assert.Equal(SecondFactorFailureReason.AccountInactive, FailureOf(result));
        Assert.Equal(1, user.Lockout.FailedAttemptCount);
        Assert.NotEqual(Totp.TimeStepAt(TestMfa.Now), factor.LastAcceptedTimeStep);
    }

    [Fact]
    public async Task Handle_TheRightCode_ResetsTheFailedAttempts()
    {
        var (user, _) = await SeedUserAsync();
        user.RecordFailedLogin(5, TimeSpan.FromMinutes(15));
        user.RecordFailedLogin(5, TimeSpan.FromMinutes(15));

        var result = await CreateHandler().Handle(new Command(_tenantId, user.Id, TestMfa.CodeAt(TestMfa.Now)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, user.Lockout.FailedAttemptCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_RefusesADisabledOrDeletedUser(bool deleted)
    {
        var (user, _) = await SeedUserAsync();
        if (deleted) user.SoftDelete();
        else user.Disable("reason");

        var result = await CreateHandler().Handle(new Command(_tenantId, user.Id, TestMfa.CodeAt(TestMfa.Now)), CancellationToken.None);

        Assert.Equal(SecondFactorFailureReason.AccountInactive, FailureOf(result));
    }

    [Fact]
    public async Task Handle_RefusesAnotherTenantsUser()
    {
        var (user, _) = await SeedUserAsync();

        var result = await CreateHandler().Handle(new Command(Guid.NewGuid(), user.Id, TestMfa.CodeAt(TestMfa.Now)), CancellationToken.None);

        Assert.Equal(SecondFactorFailureReason.AccountInactive, FailureOf(result));
    }

    [Fact]
    public async Task Handle_IsNotEnrolled_WithoutAnEnabledFactor()
    {
        var user = TestUsers.CreatePendingVerification(_tenantId);
        user.BeginMfaEnrollment(MfaFactor.Create(MfaFactorType.Totp, new FakeTotpSecretProtector().Protect(TestMfa.Secret), TestMfa.Now));
        await _repository.CreateAsync(user, user.Id);

        var result = await CreateHandler().Handle(new Command(_tenantId, user.Id, TestMfa.CodeAt(TestMfa.Now)), CancellationToken.None);

        Assert.Equal(SecondFactorFailureReason.NotEnrolled, FailureOf(result));
        Assert.Equal(0, user.Lockout.FailedAttemptCount);
    }

    [Fact]
    public async Task Handle_ChecksEveryEnabledFactor()
    {
        var (user, _) = await SeedUserAsync();
        var otherSecret = "abcdefghijabcdefghij"u8.ToArray();
        var other = user.AddEnabledTotp(otherSecret);

        var result = await CreateHandler().Handle(new Command(_tenantId, user.Id, TestMfa.CodeAt(TestMfa.Now, otherSecret)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TestMfa.Now, other.LastUsedAtUtc);
    }

    [Fact]
    public async Task Handle_AFactorWhoseSecretCantBeRead_NeverMatches()
    {
        var user = TestUsers.CreatePendingVerification(_tenantId);
        var legacy = MfaFactor.Create(MfaFactorType.Totp, "opaque-legacy-value", TestMfa.Now);
        user.BeginMfaEnrollment(legacy);
        user.CompleteMfaEnrollment(legacy.Id, 1, [], TestMfa.Now);
        await _repository.CreateAsync(user, user.Id);

        var result = await CreateHandler().Handle(new Command(_tenantId, user.Id, "123456"), CancellationToken.None);

        Assert.Equal(SecondFactorFailureReason.InvalidCode, FailureOf(result));
    }

    [Fact]
    public void Read_UntaggedErrors_IsNull()
    {
        Assert.Null(SecondFactorFailure.Read([new Error("boom")]));
    }

    // Every try lost its race: refused like a wrong code, with the same generic error.
    [Fact]
    public async Task Handle_IsAnInvalidCode_WhenEverySaveConflicts()
    {
        var (user, _) = await SeedUserAsync();
        _repository.ConflictsToSimulate = int.MaxValue;

        var result = await CreateHandler().Handle(new Command(_tenantId, user.Id, TestMfa.CodeAt(TestMfa.Now)), CancellationToken.None);

        Assert.Equal(SecondFactorFailureReason.InvalidCode, FailureOf(result));
        Assert.Equal(UserErrors.MfaCodeInvalid().Message, result.Errors[0].Message);
    }
}
