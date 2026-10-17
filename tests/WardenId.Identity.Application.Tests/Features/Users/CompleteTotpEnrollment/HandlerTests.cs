using Microsoft.Extensions.Time.Testing;
using WardenId.Identity.Application.Features.Users.CompleteTotpEnrollment;
using WardenId.Identity.Application.Mfa;
using Command = WardenId.Identity.Integration.Command.Features.Users.CompleteTotpEnrollment.Command;

namespace WardenId.Identity.Application.Tests.Features.Users.CompleteTotpEnrollment;

public class HandlerTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly FakeUserRepository _repository = new();
    private readonly FakeTotpSecretProtector _protector = new();

    private Handler CreateHandler() => new(_repository, _protector, new FakeTimeProvider(TestMfa.Now));

    private async Task<(User User, MfaFactor Factor)> SeedPendingEnrollmentAsync()
    {
        var user = TestUsers.CreatePendingVerification(_tenantId);
        user.AddRequiredAction(RequiredAction.EnrollMfa);
        var factor = MfaFactor.Create(MfaFactorType.Totp, _protector.Protect(TestMfa.Secret), TestMfa.Now);
        user.BeginMfaEnrollment(factor);
        await _repository.CreateAsync(user, user.Id);
        return (user, factor);
    }

    [Fact]
    public async Task Handle_EnablesTheFactor_ClearsEnrollMfa_AndIssuesTenRecoveryCodes()
    {
        var (user, factor) = await SeedPendingEnrollmentAsync();

        var result = await CreateHandler().Handle(
            new Command(_tenantId, user.Id, factor.Id, TestMfa.CodeAt(TestMfa.Now)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(factor.Enabled);
        Assert.Equal(Totp.TimeStepAt(TestMfa.Now), factor.LastAcceptedTimeStep);
        Assert.DoesNotContain(RequiredAction.EnrollMfa, user.PendingActions);
        Assert.Equal(10, result.Value.RecoveryCodes.Count);
        Assert.Equal(10, user.RemainingRecoveryCodeCount);
        Assert.All(result.Value.RecoveryCodes, code =>
            Assert.Contains(user.RecoveryCodes, c => c.Matches(RecoveryCodes.Hash(RecoveryCodes.Normalize(code)!))));
        Assert.Equal(1, _repository.UpdateCallCount);
    }

    [Fact]
    public async Task Handle_ReplacesTheRecoveryCodesOfAnEarlierFactor()
    {
        var (user, factor) = await SeedPendingEnrollmentAsync();
        user.AddEnabledTotp(recoveryCodes: [RecoveryCode.Create("old")]);
        // AddEnabledTotp replaced the pending factor; start a new one.
        var pending = MfaFactor.Create(MfaFactorType.Totp, _protector.Protect(TestMfa.Secret), TestMfa.Now);
        user.BeginMfaEnrollment(pending);

        var result = await CreateHandler().Handle(
            new Command(_tenantId, user.Id, pending.Id, TestMfa.CodeAt(TestMfa.Now)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(user.RecoveryCodes, c => c.Matches("old"));
        Assert.Equal(10, user.RecoveryCodes.Count);
        Assert.DoesNotContain(user.MfaFactors, f => f.Id == factor.Id);
    }

    [Fact]
    public async Task Handle_Fails_OnAWrongCode_AndLeavesTheFactorToTryAgain()
    {
        var (user, factor) = await SeedPendingEnrollmentAsync();

        var result = await CreateHandler().Handle(
            new Command(_tenantId, user.Id, factor.Id, TestMfa.CodeAt(TestMfa.Now.AddMinutes(5))), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.False(factor.Enabled);
        Assert.Contains(RequiredAction.EnrollMfa, user.PendingActions);
        Assert.Equal(0, user.Lockout.FailedAttemptCount);
        Assert.Equal(0, _repository.UpdateCallCount);
    }

    [Fact]
    public async Task Handle_Fails_ForAnEnabledFactor()
    {
        var user = TestUsers.CreatePendingVerification(_tenantId);
        var factor = user.AddEnabledTotp();
        await _repository.CreateAsync(user, user.Id);

        var result = await CreateHandler().Handle(
            new Command(_tenantId, user.Id, factor.Id, TestMfa.CodeAt(TestMfa.Now)), CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task Handle_Fails_ForAnUnknownFactor()
    {
        var (user, _) = await SeedPendingEnrollmentAsync();

        var result = await CreateHandler().Handle(
            new Command(_tenantId, user.Id, Guid.NewGuid(), TestMfa.CodeAt(TestMfa.Now)), CancellationToken.None);

        Assert.True(result.IsFailed);
    }
}
