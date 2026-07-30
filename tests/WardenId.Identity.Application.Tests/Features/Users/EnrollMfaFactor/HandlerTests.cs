using WardenId.Identity.Application.Features.Users.EnrollMfaFactor;

namespace WardenId.Identity.Application.Tests.Features.Users.EnrollMfaFactor;

public class HandlerTests
{
    [Fact]
    public async Task Handle_EnrollsFactor_AndReturnsTenRawRecoveryCodes()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        var repository = new FakeUserRepository();
        // Seeded with a different actor so the post-Handle actor assertion proves the handler set it.
        await repository.CreateAsync(user, Guid.NewGuid(), CancellationToken.None);
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(tenantId, user.Id, MfaFactorType.Totp, "opaque-blob"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.Value.RawRecoveryCodes.Count);
        Assert.Equal(10, result.Value.RawRecoveryCodes.Distinct().Count());
        Assert.Single(user.MfaFactors);
        Assert.Equal(10, user.RecoveryCodes.Count);
        Assert.Equal(1, repository.UpdateCallCount);
        Assert.Equal(user.Id, repository.LastActorId!.Value);
    }

    [Fact]
    public async Task Handle_Fails_WhenUserNotFound()
    {
        var handler = new Handler(new FakeUserRepository());

        var result = await handler.Handle(new Command(Guid.NewGuid(), Guid.NewGuid(), MfaFactorType.Totp, "opaque-blob"), CancellationToken.None);

        Assert.True(result.IsFailed);
    }
}
