using Microsoft.Extensions.Options;
using WardenId.Identity.Application.Features.Users.RevokeMfaFactor;

namespace WardenId.Identity.Application.Tests.Features.Users.RevokeMfaFactor;

public class HandlerTests
{
    [Fact]
    public async Task Handle_RevokesFactor_WhenMfaIsOptional()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        var factor = MfaFactor.Create(MfaFactorType.Totp, "opaque-blob");
        factor.Verify();
        user.EnrollMfaFactor(factor, []);
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var handler = new Handler(repository, Options.Create(new IdentityPolicyOptions { MfaPolicyMode = MfaPolicyMode.Optional }));

        var result = await handler.Handle(new Command(tenantId, user.Id, factor.Id, Guid.NewGuid(), ActorIsAdmin: false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(user.MfaFactors);
    }

    [Fact]
    public async Task Handle_Fails_WhenRevokingLastFactorAndMfaIsRequired()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        var factor = MfaFactor.Create(MfaFactorType.Totp, "opaque-blob");
        factor.Verify();
        user.EnrollMfaFactor(factor, []);
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var handler = new Handler(repository, Options.Create(new IdentityPolicyOptions { MfaPolicyMode = MfaPolicyMode.Required }));

        var result = await handler.Handle(new Command(tenantId, user.Id, factor.Id, Guid.NewGuid(), ActorIsAdmin: false), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Single(user.MfaFactors);
    }
}
