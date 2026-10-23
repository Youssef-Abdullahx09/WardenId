namespace WardenId.Identity.Domain.Tests;

public class UserDomainEventActorTests
{
    [Fact]
    public void GetDomainEvents_StampsTheGivenActor_AndTheAggregateId()
    {
        var actor = Guid.NewGuid();
        var user = User.Register(Guid.NewGuid(), Email.Create("someone@example.com").Value, Username.Create("someone").Value).Value;
        Assert.True(user.Disable("policy violation").IsSuccess);

        var events = user.GetDomainEvents(actor);

        Assert.NotEmpty(events);
        Assert.All(events, e => Assert.Equal(actor, e.UserId));
        Assert.All(events, e => Assert.Equal(user.Id, e.AggregateId));
    }
}
