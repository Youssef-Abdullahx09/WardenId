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
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(tenantId, user.Id, factor.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(user.MfaFactors.Single().Enabled);
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
    }
}
