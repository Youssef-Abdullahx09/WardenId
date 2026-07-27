namespace WardenId.Identity.Domain.Tests;

public class UserAttributesAndActionsTests
{
    private static User CreateUser() =>
        User.Register(Guid.NewGuid(), Email.Create("someone@example.com").Value, Username.Create("someone").Value).Value;

    [Fact]
    public void SetAttribute_AddsNewAttribute()
    {
        var user = CreateUser();

        var result = user.SetAttribute("department", "engineering", UserAttributeValueType.String);

        Assert.True(result.IsSuccess);
        Assert.Single(user.Attributes);
    }

    [Fact]
    public void SetAttribute_UpdatesExistingAttributeByKey()
    {
        var user = CreateUser();
        user.SetAttribute("department", "engineering", UserAttributeValueType.String);

        var result = user.SetAttribute("department", "sales", UserAttributeValueType.String);

        Assert.True(result.IsSuccess);
        Assert.Single(user.Attributes);
        Assert.Equal("sales", user.Attributes.Single().Value);
    }

    [Fact]
    public void SetAttribute_FailsOnEmptyKey()
    {
        var user = CreateUser();

        var result = user.SetAttribute("", "value", UserAttributeValueType.String);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void AddRequiredAction_DedupesOnRepeatedAdd()
    {
        var user = CreateUser();

        user.AddRequiredAction(RequiredAction.AcceptTos);
        user.AddRequiredAction(RequiredAction.AcceptTos);

        Assert.Single(user.PendingActions, a => a == RequiredAction.AcceptTos);
    }

    [Fact]
    public void ClearRequiredAction_RemovesActionIfPresent()
    {
        var user = CreateUser();
        user.AddRequiredAction(RequiredAction.AcceptTos);

        user.ClearRequiredAction(RequiredAction.AcceptTos);

        Assert.DoesNotContain(RequiredAction.AcceptTos, user.PendingActions);
    }

    [Fact]
    public void ClearRequiredAction_NoOpWhenActionNotPresent()
    {
        var user = CreateUser();

        user.ClearRequiredAction(RequiredAction.AcceptTos);

        Assert.DoesNotContain(RequiredAction.AcceptTos, user.PendingActions);
    }
}
