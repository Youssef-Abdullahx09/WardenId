using WardenId.Identity.Domain.Events;

namespace WardenId.Identity.Domain.Tests;

public class UserPersonNameTests
{
    private static PersonName Name(string value) => PersonName.FromTrustedSource(value);

    private static User NewUser(PersonName? firstName = null, PersonName? lastName = null) =>
        User.Register(
            Guid.NewGuid(), Email.Create("someone@example.com").Value, Username.Create("someone").Value,
            firstName, lastName).Value;

    [Fact]
    public void Register_KeepsTheNames()
    {
        var user = NewUser(Name("Ada"), Name("Lovelace"));

        Assert.Equal(Name("Ada"), user.FirstName);
        Assert.Equal(Name("Lovelace"), user.LastName);
    }

    [Fact]
    public void Register_WithoutNames_HasNone()
    {
        var user = NewUser();

        Assert.Null(user.FirstName);
        Assert.Null(user.LastName);
    }

    [Fact]
    public void ChangePersonName_SetsBothNames_AndRaisesTheEvent()
    {
        var user = NewUser();
        user.ClearDomainEvents();

        var result = user.ChangePersonName(Name("Ada"), Name("Lovelace"));

        Assert.True(result.IsSuccess);
        Assert.Equal(Name("Ada"), user.FirstName);
        Assert.Equal(Name("Lovelace"), user.LastName);
        var domainEvent = Assert.Single(user.DomainEvents.OfType<UserPersonNameChangedDomainEvent>());
        Assert.Equal(Name("Ada"), domainEvent.FirstName);
        Assert.Equal(Name("Lovelace"), domainEvent.LastName);
    }

    [Fact]
    public void ChangePersonName_WithNull_RemovesTheName()
    {
        var user = NewUser(Name("Ada"), Name("Lovelace"));

        Assert.True(user.ChangePersonName(Name("Ada"), null).IsSuccess);

        Assert.Equal(Name("Ada"), user.FirstName);
        Assert.Null(user.LastName);
    }

    [Fact]
    public void ChangePersonName_ToTheSameNames_ChangesNothing()
    {
        var user = NewUser(Name("Ada"), null);
        user.ClearDomainEvents();

        Assert.True(user.ChangePersonName(Name("Ada"), null).IsSuccess);

        Assert.Empty(user.DomainEvents);
    }

    [Fact]
    public void ChangePersonName_Fails_WhenDisabled()
    {
        var user = NewUser();
        user.Disable("investigation");

        var result = user.ChangePersonName(Name("Ada"), null);

        Assert.True(result.IsFailed);
        Assert.Null(user.FirstName);
    }
}
