namespace WardenId.Identity.Domain.Tests;

public class UserLifecycleTests
{
    private static User CreateUser() =>
        User.Register(Guid.NewGuid(), Email.Create("someone@example.com").Value, Username.Create("someone").Value).Value;

    [Fact]
    public void Disable_FailsWithoutReason()
    {
        var user = CreateUser();

        var result = user.Disable("");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Disable_SetsStatusAndRaisesEvent()
    {
        var user = CreateUser();

        var result = user.Disable("policy violation");

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Disabled, user.Status);
        Assert.Contains(user.DomainEvents, e => e is UserDisabledDomainEvent d && d.Reason == "policy violation");
    }

    [Fact]
    public void Disable_FailsWhenAlreadyDisabled()
    {
        var user = CreateUser();
        user.Disable("first reason");

        var result = user.Disable("second reason");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void SoftDelete_SetsStatusAndRaisesEvent()
    {
        var user = CreateUser();

        var result = user.SoftDelete();

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.SoftDeleted, user.Status);
        Assert.Contains(user.DomainEvents, e => e is UserSoftDeletedDomainEvent);
    }

    [Fact]
    public void SoftDelete_FailsWhenAlreadySoftDeleted()
    {
        var user = CreateUser();
        user.SoftDelete();

        var result = user.SoftDelete();

        Assert.True(result.IsFailed);
    }
}
