using WardenId.Identity.Application.Features.Users.RevokeMfaFactor;

namespace WardenId.Identity.Application.Tests.Features.Users.RevokeMfaFactor;

public class HandlerTests
{
    private static async Task<(User User, MfaFactor Factor, FakeUserRepository Repository)> SeedUserWithEnabledFactorAsync(Guid tenantId)
    {
        var user = TestUsers.CreatePendingVerification(tenantId);
        var factor = user.AddEnabledTotp();
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
        var handler = new Handler(repository, TestPolicies.Resolver(new IdentityPolicyOptions { MfaPolicyMode = MfaPolicyMode.Optional }));

        var result = await handler.Handle(new Command(tenantId, user.Id, factor.Id, actorUserId), CancellationToken.None);

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
        var handler = new Handler(repository, TestPolicies.Resolver(new IdentityPolicyOptions { MfaPolicyMode = MfaPolicyMode.Required }));

        var result = await handler.Handle(new Command(tenantId, user.Id, factor.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Single(user.MfaFactors);
        Assert.Equal(0, repository.UpdateCallCount);
    }

    // No user is known to be an admin, so the policy can't be relaxed for "non-admins":
    // RequiredForAdmins protects the last factor of every user, like Required.
    [Fact]
    public async Task Handle_Fails_WhenRevokingLastFactorAndPolicyIsRequiredForAdmins()
    {
        var tenantId = Guid.NewGuid();
        var (user, factor, repository) = await SeedUserWithEnabledFactorAsync(tenantId);
        var handler = new Handler(repository, TestPolicies.Resolver(new IdentityPolicyOptions { MfaPolicyMode = MfaPolicyMode.RequiredForAdmins }));

        var result = await handler.Handle(new Command(tenantId, user.Id, factor.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Single(user.MfaFactors);
        Assert.Equal(0, repository.UpdateCallCount);
    }

    [Fact]
    public async Task Handle_RevokesFactor_WhenPolicyIsRequiredForAdmins_AndAnotherEnabledFactorRemains()
    {
        var tenantId = Guid.NewGuid();
        var (user, factor, repository) = await SeedUserWithEnabledFactorAsync(tenantId);
        var second = user.AddEnabledTotp();
        var handler = new Handler(repository, TestPolicies.Resolver(new IdentityPolicyOptions { MfaPolicyMode = MfaPolicyMode.RequiredForAdmins }));

        var result = await handler.Handle(new Command(tenantId, user.Id, factor.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(second.Id, Assert.Single(user.MfaFactors).Id);
    }

    [Fact]
    public async Task Handle_Fails_WhenUserIsDisabled()
    {
        var tenantId = Guid.NewGuid();
        var (user, factor, repository) = await SeedUserWithEnabledFactorAsync(tenantId);
        user.Disable("policy violation");
        var handler = new Handler(repository, TestPolicies.Resolver(new IdentityPolicyOptions { MfaPolicyMode = MfaPolicyMode.Optional }));

        var result = await handler.Handle(new Command(tenantId, user.Id, factor.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Single(user.MfaFactors);
        Assert.Equal(0, repository.UpdateCallCount);
    }

    [Fact]
    public async Task Handle_Fails_WhenTheTenantRequiresMfa_OverAGlobalOptional()
    {
        var tenantId = Guid.NewGuid();
        var (user, factor, repository) = await SeedUserWithEnabledFactorAsync(tenantId);
        var tenantPolicies = new FakeIdentityTenantPolicyRepository()
            .With(tenantId, IdentityPolicyOverrides.None with { MfaMode = MfaPolicyMode.Required });
        var handler = new Handler(repository,
            TestPolicies.Resolver(new IdentityPolicyOptions { MfaPolicyMode = MfaPolicyMode.Optional }, tenantPolicies));

        var result = await handler.Handle(new Command(tenantId, user.Id, factor.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Single(user.MfaFactors);
    }
}
