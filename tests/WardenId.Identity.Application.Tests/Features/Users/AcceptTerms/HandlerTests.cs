using Microsoft.Extensions.Time.Testing;
using WardenId.Identity.Application.Features.Users.AcceptTerms;
using Command = WardenId.Identity.Integration.Command.Features.Users.AcceptTerms.Command;

namespace WardenId.Identity.Application.Tests.Features.Users.AcceptTerms;

public class HandlerTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly FakeUserRepository _repository = new();

    private Handler CreateHandler() => new(_repository, new FakeTimeProvider(TestMfa.Now));

    private async Task<User> SeedUserAsync()
    {
        var user = TestUsers.CreatePendingVerification(_tenantId);
        user.AddRequiredAction(RequiredAction.AcceptTos);
        await _repository.CreateAsync(user, Guid.NewGuid());
        return user;
    }

    [Fact]
    public async Task Handle_RecordsTheAcceptance_AndClearsAcceptTos()
    {
        var user = await SeedUserAsync();

        var result = await CreateHandler().Handle(new Command(_tenantId, user.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TestMfa.Now, user.TermsAcceptedAtUtc);
        Assert.DoesNotContain(RequiredAction.AcceptTos, user.PendingActions);
        Assert.Equal(user.Id, _repository.LastActorId);
    }

    [Fact]
    public async Task Handle_TriesAgain_AfterASaveConflict()
    {
        var user = await SeedUserAsync();
        _repository.ConflictsToSimulate = 1;

        var result = await CreateHandler().Handle(new Command(_tenantId, user.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, _repository.UpdateCallCount);
    }

    [Fact]
    public async Task Handle_Fails_ForAnotherTenantsUser()
    {
        var user = await SeedUserAsync();

        var result = await CreateHandler().Handle(new Command(Guid.NewGuid(), user.Id), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Null(user.TermsAcceptedAtUtc);
    }

    [Fact]
    public async Task Handle_Fails_ForADisabledUser()
    {
        var user = await SeedUserAsync();
        user.Disable("reason");

        var result = await CreateHandler().Handle(new Command(_tenantId, user.Id), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(0, _repository.UpdateCallCount);
    }
}
