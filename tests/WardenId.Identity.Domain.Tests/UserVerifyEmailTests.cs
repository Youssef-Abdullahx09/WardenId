using WardenId.Identity.Domain.Events;

namespace WardenId.Identity.Domain.Tests;

public class UserVerifyEmailTests
{
    private static User CreateRegisteredUser() =>
        User.Register(Guid.NewGuid(), Email.Create("someone@example.com").Value, Username.Create("someone").Value).Value;

    [Fact]
    public void VerifyEmail_FailsWhenTokenDoesNotMatch()
    {
        var user = CreateRegisteredUser();
        user.AddEmailVerificationToken(EmailVerificationToken.Create("hash-1", DateTimeOffset.UtcNow.AddHours(1)));

        var result = user.VerifyEmail("wrong-hash");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void VerifyEmail_FailsWhenTokenExpired()
    {
        var user = CreateRegisteredUser();
        user.AddEmailVerificationToken(EmailVerificationToken.Create("hash-1", DateTimeOffset.UtcNow.AddHours(-1)));

        var result = user.VerifyEmail("hash-1");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void VerifyEmail_SucceedsAndActivatesUser()
    {
        var user = CreateRegisteredUser();
        user.AddEmailVerificationToken(EmailVerificationToken.Create("hash-1", DateTimeOffset.UtcNow.AddHours(1)));

        var result = user.VerifyEmail("hash-1");

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.DoesNotContain(RequiredAction.VerifyEmail, user.PendingActions);
    }

    [Fact]
    public void VerifyEmail_RaisesUserEmailVerifiedDomainEvent()
    {
        var user = CreateRegisteredUser();
        user.AddEmailVerificationToken(EmailVerificationToken.Create("hash-1", DateTimeOffset.UtcNow.AddHours(1)));

        user.VerifyEmail("hash-1");

        Assert.Contains(user.DomainEvents, e => e is UserEmailVerifiedDomainEvent);
    }

    [Fact]
    public void VerifyEmail_FailsWhenUserAlreadyActive()
    {
        var user = CreateRegisteredUser();
        user.AddEmailVerificationToken(EmailVerificationToken.Create("hash-1", DateTimeOffset.UtcNow.AddHours(1)));
        user.VerifyEmail("hash-1");

        var result = user.VerifyEmail("hash-1");

        Assert.True(result.IsFailed);
    }
}
