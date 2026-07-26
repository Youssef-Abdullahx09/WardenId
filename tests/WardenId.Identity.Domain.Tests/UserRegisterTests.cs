namespace WardenId.Identity.Domain.Tests;

public class UserRegisterTests
{
    private static (Email Email, Username Username) ValidInput() =>
        (Email.Create("someone@example.com").Value, Username.Create("someone").Value);

    [Fact]
    public void Register_ThrowsWhenTenantIdIsEmpty()
    {
        var (email, username) = ValidInput();

        Assert.Throws<ArgumentException>(() => User.Register(Guid.Empty, email, username));
    }

    [Fact]
    public void Register_SetsStatusToPendingVerification()
    {
        var (email, username) = ValidInput();

        var result = User.Register(Guid.NewGuid(), email, username);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.PendingVerification, result.Value.Status);
    }

    [Fact]
    public void Register_AddsVerifyEmailRequiredAction()
    {
        var (email, username) = ValidInput();

        var result = User.Register(Guid.NewGuid(), email, username);

        Assert.Contains(RequiredAction.VerifyEmail, result.Value.PendingActions);
    }

    [Fact]
    public void Register_RaisesUserRegisteredDomainEvent()
    {
        var (email, username) = ValidInput();

        var result = User.Register(Guid.NewGuid(), email, username);

        var domainEvent = Assert.Single(result.Value.DomainEvents);
        Assert.IsType<UserRegisteredDomainEvent>(domainEvent);
    }

    [Fact]
    public void Register_HasNoCredentialYet()
    {
        var (email, username) = ValidInput();

        var result = User.Register(Guid.NewGuid(), email, username);

        Assert.Null(result.Value.Credential);
    }
}
