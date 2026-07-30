using WardenId.Identity.Application.Features.Users.VerifyMfaFactor;

namespace WardenId.Identity.Application.Tests.Features.Users.VerifyMfaFactor;

public class HandlerTests
{
    [Fact]
    public async Task Handle_VerifiesFactor_AndEnablesIt()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        var factor = MfaFactor.Create(MfaFactorType.Totp, "opaque-blob");
        user.EnrollMfaFactor(factor, []);
        var repository = new FakeUserRepository();
        // Seeded with a different actor so the post-Handle assertion proves the handler set it.
        await repository.CreateAsync(user, Guid.NewGuid(), CancellationToken.None);
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(tenantId, user.Id, factor.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(user.MfaFactors.Single().Enabled);
        Assert.Equal(1, repository.UpdateCallCount);
        Assert.Equal(user.Id, repository.LastActorId!.Value);
    }

    [Fact]
    public async Task Handle_Fails_WhenFactorNotFound()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(tenantId, user.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(0, repository.UpdateCallCount);
    }

    [Fact]
    public async Task Handle_Fails_WhenUserIsDisabled()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        var factor = MfaFactor.Create(MfaFactorType.Totp, "opaque-blob");
        user.EnrollMfaFactor(factor, []);
        user.Disable("policy violation");
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(tenantId, user.Id, factor.Id), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.False(user.MfaFactors.Single().Enabled);
        Assert.Equal(0, repository.UpdateCallCount);
    }
}
