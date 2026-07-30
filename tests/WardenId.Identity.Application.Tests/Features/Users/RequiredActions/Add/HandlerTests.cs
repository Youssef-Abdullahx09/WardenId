using WardenId.Identity.Application.Features.Users.RequiredActions.Add;

namespace WardenId.Identity.Application.Tests.Features.Users.RequiredActions.Add;

public class HandlerTests
{
    [Fact]
    public async Task Handle_AddsRequiredAction()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(tenantId, user.Id, RequiredAction.AcceptTos, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains(RequiredAction.AcceptTos, user.PendingActions);
    }
}
