using Microsoft.Extensions.Options;
using WardenId.Identity.Application.Features.Users.RevokeMfaFactor;

namespace WardenId.Identity.Application.Tests.Features.Users.RevokeMfaFactor;

public class HandlerTests
{
    private static async Task<(User User, MfaFactor Factor, FakeUserRepository Repository)> SeedUserWithEnabledFactorAsync(Guid tenantId)
    {
        var user = TestUsers.CreatePendingVerification(tenantId);
        var factor = MfaFactor.Create(MfaFactorType.Totp, "opaque-blob");
        factor.Verify();
        user.EnrollMfaFactor(factor, []);
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        return (user, factor, repository);
    }

    [Fact]
    public async Task Handle_RevokesFactor_WhenMfaIsOptional()
    {
        var tenantId = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();
        var (user, factor, repository) = await SeedUserWithEnabledFactorAsync(tenantId);
        var handler = new Handler(repository, Options.Create(new IdentityPolicyOptions { MfaPolicyMode = MfaPolicyMode.Optional }));

        var result = await handler.Handle(new Command(tenantId, user.Id, factor.Id, actorUserId, TargetUserIsAdmin: false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(user.MfaFactors);
        Assert.Equal(1, repository.UpdateCallCount);
        Assert.Equal(actorUserId, repository.LastActorId!.Value);
    }

    [Fact]
    public async Task Handle_Fails_WhenRevokingLastFactorAndMfaIsRequired()
    {
        var tenantId = Guid.NewGuid();
        var (user, factor, repository) = await SeedUserWithEnabledFactorAsync(tenantId);
        var handler = new Handler(repository, Options.Create(new IdentityPolicyOptions { MfaPolicyMode = MfaPolicyMode.Required }));

        var result = await handler.Handle(new Command(tenantId, user.Id, factor.Id, Guid.NewGuid(), TargetUserIsAdmin: false), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Single(user.MfaFactors);
        Assert.Equal(0, repository.UpdateCallCount);
    }

    [Fact]
    public async Task Handle_RevokesLastFactor_WhenPolicyIsRequiredForAdmins_AndTargetUserIsNotAnAdmin()
    {
        var tenantId = Guid.NewGuid();
        var (user, factor, repository) = await SeedUserWithEnabledFactorAsync(tenantId);
        var handler = new Handler(repository, Options.Create(new IdentityPolicyOptions { MfaPolicyMode = MfaPolicyMode.RequiredForAdmins }));

        var result = await handler.Handle(new Command(tenantId, user.Id, factor.Id, Guid.NewGuid(), TargetUserIsAdmin: false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(user.MfaFactors);
    }

    [Fact]
    public async Task Handle_Fails_WhenPolicyIsRequiredForAdmins_AndTargetUserIsAnAdminWithOnlyOneEnabledFactor()
    {
        var tenantId = Guid.NewGuid();
        var (user, factor, repository) = await SeedUserWithEnabledFactorAsync(tenantId);
        var handler = new Handler(repository, Options.Create(new IdentityPolicyOptions { MfaPolicyMode = MfaPolicyMode.RequiredForAdmins }));

        var result = await handler.Handle(new Command(tenantId, user.Id, factor.Id, Guid.NewGuid(), TargetUserIsAdmin: true), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Single(user.MfaFactors);
        Assert.Equal(0, repository.UpdateCallCount);
    }

    [Fact]
    public async Task Handle_Fails_WhenUserIsDisabled()
    {
        var tenantId = Guid.NewGuid();
        var (user, factor, repository) = await SeedUserWithEnabledFactorAsync(tenantId);
        user.Disable("policy violation");
        var handler = new Handler(repository, Options.Create(new IdentityPolicyOptions { MfaPolicyMode = MfaPolicyMode.Optional }));

        var result = await handler.Handle(new Command(tenantId, user.Id, factor.Id, Guid.NewGuid(), TargetUserIsAdmin: false), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Single(user.MfaFactors);
        Assert.Equal(0, repository.UpdateCallCount);
    }
}
