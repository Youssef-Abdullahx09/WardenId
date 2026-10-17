using WardenId.Identity.Application.Features.Users.ResetMfa;

namespace WardenId.Identity.Application.Tests.Features.Users.ResetMfa;

public class HandlerTests
{
    [Fact]
    public async Task Handle_RemovesFactorsAndCodes_AndAsksForEnrollment_AsTheActor()
    {
        var tenantId = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        user.AddEnabledTotp(recoveryCodes: [RecoveryCode.Create("hash-1")]);
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id);

        var result = await new Handler(repository).Handle(new Command(tenantId, user.Id, actorUserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(user.MfaFactors);
        Assert.Empty(user.RecoveryCodes);
        Assert.Contains(RequiredAction.EnrollMfa, user.PendingActions);
        Assert.Equal(actorUserId, repository.LastActorId);
    }

    [Fact]
    public async Task Handle_Fails_ForAnUnknownUser()
    {
        var result = await new Handler(new FakeUserRepository())
            .Handle(new Command(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(UserErrorCodes.NotFound, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
    }

    [Fact]
    public async Task Handle_FailsWithAConflict_WhenTheSaveConflicts()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id);
        repository.ConflictsToSimulate = 1;

        var result = await new Handler(repository).Handle(new Command(tenantId, user.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(UserErrorCodes.Conflict, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
    }
}
