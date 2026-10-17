using Microsoft.Extensions.Time.Testing;
using WardenId.Identity.Application.Features.Users.RegenerateRecoveryCodes;
using WardenId.Identity.Application.Mfa;
using WardenId.Identity.Integration.Command.Features.Users.VerifySecondFactor;
using Command = WardenId.Identity.Integration.Command.Features.Users.RegenerateRecoveryCodes.Command;

namespace WardenId.Identity.Application.Tests.Features.Users.RegenerateRecoveryCodes;

public class HandlerTests
{
    private const string RecoveryCode = "abcde-fgh23";

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly FakeUserRepository _repository = new();

    private Handler CreateHandler(IdentityPolicyOptions? options = null) =>
        new(_repository, TestPolicies.Resolver(options ?? new IdentityPolicyOptions()), new FakeTotpSecretProtector(), new FakeTimeProvider(TestMfa.Now));

    private async Task<User> SeedUserAsync()
    {
        var user = TestUsers.CreatePendingVerification(_tenantId);
        user.AddEnabledTotp(recoveryCodes: [Domain.RecoveryCode.Create(RecoveryCodes.Hash(RecoveryCodes.Normalize(RecoveryCode)!))]);
        await _repository.CreateAsync(user, user.Id);
        return user;
    }

    [Fact]
    public async Task Handle_ReplacesEveryRecoveryCode_AndReturnsTheNewOnesOnce()
    {
        var user = await SeedUserAsync();

        var result = await CreateHandler().Handle(new Command(_tenantId, user.Id, TestMfa.CodeAt(TestMfa.Now)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(RecoveryCodes.Count, result.Value.Count);
        Assert.Equal(RecoveryCodes.Count, user.RemainingRecoveryCodeCount);
        Assert.All(result.Value, code => Assert.Contains(user.RecoveryCodes, c => c.Matches(RecoveryCodes.Hash(RecoveryCodes.Normalize(code)!))));
        Assert.DoesNotContain(user.RecoveryCodes, c => c.Matches(RecoveryCodes.Hash(RecoveryCodes.Normalize(RecoveryCode)!)));
        Assert.Equal(1, _repository.UpdateCallCount);
    }

    [Fact]
    public async Task Handle_RefusesARecoveryCode_AsAWrongCode()
    {
        var user = await SeedUserAsync();

        var result = await CreateHandler().Handle(new Command(_tenantId, user.Id, RecoveryCode), CancellationToken.None);

        Assert.Equal(SecondFactorFailureReason.InvalidCode, SecondFactorFailure.Read(result.Errors));
        Assert.Equal(1, user.Lockout.FailedAttemptCount);
        Assert.Equal(1, user.RemainingRecoveryCodeCount);
    }

    [Fact]
    public async Task Handle_LocksTheAccount_OnTheTenantsMaxFailedAttempts()
    {
        var user = await SeedUserAsync();
        var handler = CreateHandler(new IdentityPolicyOptions { MaxFailedLoginAttempts = 1 });

        var result = await handler.Handle(new Command(_tenantId, user.Id, "000000"), CancellationToken.None);

        Assert.Equal(SecondFactorFailureReason.LockedOut, SecondFactorFailure.Read(result.Errors));
        Assert.Equal(UserStatus.Locked, user.Status);
    }

    [Fact]
    public async Task Handle_IsAnInvalidCode_WhenEverySaveConflicts()
    {
        var user = await SeedUserAsync();
        _repository.ConflictsToSimulate = int.MaxValue;

        var result = await CreateHandler().Handle(new Command(_tenantId, user.Id, TestMfa.CodeAt(TestMfa.Now)), CancellationToken.None);

        Assert.Equal(SecondFactorFailureReason.InvalidCode, SecondFactorFailure.Read(result.Errors));
    }

    [Fact]
    public async Task Handle_AWrongCode_IsTaggedInvalidCode()
    {
        var user = await SeedUserAsync();

        var result = await CreateHandler().Handle(new Command(_tenantId, user.Id, "000000"), CancellationToken.None);

        Assert.Equal(SecondFactorFailureReason.InvalidCode, SecondFactorFailure.Read(result.Errors));
        Assert.Equal(UserErrors.MfaCodeInvalid().Message, result.Errors[0].Message);
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
        Assert.Equal(1, user.RemainingRecoveryCodeCount);
    }

    [Fact]
    public async Task Handle_WithoutAnEnabledFactor_IsTaggedNotEnrolled()
    {
        var user = TestUsers.CreatePendingVerification(_tenantId);
        await _repository.CreateAsync(user, user.Id);

        var result = await CreateHandler().Handle(new Command(_tenantId, user.Id, "123456"), CancellationToken.None);

        Assert.Equal(SecondFactorFailureReason.NotEnrolled, SecondFactorFailure.Read(result.Errors));
    }
}
