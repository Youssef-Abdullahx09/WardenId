using WardenId.Identity.Domain.Events;

namespace WardenId.Identity.Domain.Tests;

public class UserChangeIdentityTests
{
    private static User CreateRegisteredUser() =>
        User.Register(Guid.NewGuid(), Email.Create("someone@example.com").Value, Username.Create("someone").Value).Value;

    private static User CreateActiveUser()
    {
        var user = CreateRegisteredUser();
        user.AddEmailVerificationToken(EmailVerificationToken.Create("first", DateTimeOffset.UtcNow.AddHours(1)));
        Assert.True(user.VerifyEmail("first").IsSuccess);
        user.ClearDomainEvents();
        return user;
    }

    private static Email NewEmail => Email.Create("changed@example.com").Value;

    private static EmailVerificationToken NewToken(string hash = "second") =>
        EmailVerificationToken.Create(hash, DateTimeOffset.UtcNow.AddHours(1));

    [Fact]
    public void ChangeEmail_SetsTheNewEmail()
    {
        var user = CreateActiveUser();

        var result = user.ChangeEmail(NewEmail, NewToken());

        Assert.True(result.IsSuccess);
        Assert.Equal(NewEmail, user.Email);
    }

    [Fact]
    public void ChangeEmail_KeepsAnActiveUserActive_AndAddsTheVerifyEmailAction()
    {
        var user = CreateActiveUser();

        user.ChangeEmail(NewEmail, NewToken());

        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Contains(RequiredAction.VerifyEmail, user.PendingActions);
    }

    [Fact]
    public void ChangeEmail_KeepsAPendingUserPending_WithASingleVerifyEmailAction()
    {
        var user = CreateRegisteredUser();

        user.ChangeEmail(NewEmail, NewToken());

        Assert.Equal(UserStatus.PendingVerification, user.Status);
        Assert.Single(user.PendingActions, a => a == RequiredAction.VerifyEmail);
    }

    [Fact]
    public void ChangeEmail_DropsTheOldAddressTokens_AndKeepsOnlyTheNewOne()
    {
        var user = CreateActiveUser();
        user.AddEmailVerificationToken(NewToken("outstanding"));
        user.RequestPasswordReset(PasswordResetToken.Create("reset", DateTimeOffset.UtcNow.AddHours(1)));

        user.ChangeEmail(NewEmail, NewToken());

        Assert.Equal("second", Assert.Single(user.EmailVerificationTokens).TokenHash);
        Assert.Empty(user.PasswordResetTokens);
        Assert.True(user.VerifyEmail("outstanding").IsFailed);
    }

    [Fact]
    public void ChangeEmail_ThenVerifyEmail_WithTheNewToken_ClearsTheActionAndStaysActive()
    {
        var user = CreateActiveUser();
        user.ChangeEmail(NewEmail, NewToken());

        var result = user.VerifyEmail("second");

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.DoesNotContain(RequiredAction.VerifyEmail, user.PendingActions);
    }

    [Fact]
    public void ChangeEmail_LeavesTheEmailUnverified_SoEnableAfterDisableGoesBackToPendingVerification()
    {
        var user = CreateActiveUser();
        user.ChangeEmail(NewEmail, NewToken());
        user.Disable("investigation");

        user.Enable();

        Assert.Equal(UserStatus.PendingVerification, user.Status);
    }

    [Fact]
    public void ChangeEmail_RaisesUserEmailChanged_WithOldAndNewAddresses()
    {
        var user = CreateActiveUser();

        user.ChangeEmail(NewEmail, NewToken());

        var domainEvent = Assert.Single(user.DomainEvents.OfType<UserEmailChangedDomainEvent>());
        Assert.Equal("someone@example.com", domainEvent.PreviousEmail.Value);
        Assert.Equal(NewEmail, domainEvent.Email);
    }

    [Fact]
    public void ChangeEmail_ToTheSameEmail_ChangesNothing()
    {
        var user = CreateActiveUser();

        var result = user.ChangeEmail(Email.Create("someone@example.com").Value, NewToken());

        Assert.True(result.IsSuccess);
        Assert.Equal("first", Assert.Single(user.EmailVerificationTokens).TokenHash);
        Assert.Empty(user.PendingActions);
        Assert.Empty(user.DomainEvents);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ChangeEmail_FailsForDisabledOrSoftDeletedUsers(bool softDelete)
    {
        var user = CreateActiveUser();
        if (softDelete) user.SoftDelete(); else user.Disable("investigation");

        var result = user.ChangeEmail(NewEmail, NewToken());

        Assert.True(result.IsFailed);
        Assert.Equal("someone@example.com", user.Email.Value);
    }

    [Fact]
    public void ChangeUsername_SetsTheNewUsername_AndRaisesUserUsernameChanged()
    {
        var user = CreateActiveUser();
        var newUsername = Username.Create("renamed").Value;

        var result = user.ChangeUsername(newUsername);

        Assert.True(result.IsSuccess);
        Assert.Equal(newUsername, user.Username);
        var domainEvent = Assert.Single(user.DomainEvents.OfType<UserUsernameChangedDomainEvent>());
        Assert.Equal("someone", domainEvent.PreviousUsername.Value);
        Assert.Equal(newUsername, domainEvent.Username);
        Assert.Empty(user.PendingActions);
    }

    [Fact]
    public void ChangeUsername_ToTheSameUsername_RaisesNothing()
    {
        var user = CreateActiveUser();

        var result = user.ChangeUsername(Username.Create("someone").Value);

        Assert.True(result.IsSuccess);
        Assert.Empty(user.DomainEvents);
    }

    [Fact]
    public void ChangeUsername_FailsForDisabledUsers()
    {
        var user = CreateActiveUser();
        user.Disable("investigation");

        var result = user.ChangeUsername(Username.Create("renamed").Value);

        Assert.True(result.IsFailed);
        Assert.Equal("someone", user.Username.Value);
    }

    [Fact]
    public void VerifyEmail_SucceedsForAnActiveUser_WithTheVerifyEmailAction()
    {
        var user = CreateActiveUser();
        user.AddRequiredAction(RequiredAction.VerifyEmail);
        user.AddEmailVerificationToken(NewToken());

        var result = user.VerifyEmail("second");

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Empty(user.PendingActions);
    }

    [Fact]
    public void VerifyEmail_FailsForADisabledUser()
    {
        var user = CreateRegisteredUser();
        user.AddEmailVerificationToken(NewToken());
        user.Disable("investigation");

        var result = user.VerifyEmail("second");

        Assert.True(result.IsFailed);
        Assert.Equal(UserStatus.Disabled, user.Status);
    }
}
