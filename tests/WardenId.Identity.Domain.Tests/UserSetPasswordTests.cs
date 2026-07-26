namespace WardenId.Identity.Domain.Tests;

public class UserSetPasswordTests
{
    private static User CreateUser() =>
        User.Register(Guid.NewGuid(), Email.Create("someone@example.com").Value, Username.Create("someone").Value).Value;

    private static PasswordPolicy PolicyWithHistory(int historyCount) =>
        PasswordPolicy.Create(8, true, true, false, false, historyCount).Value;

    [Fact]
    public void SetPassword_SetsCredentialOnFirstCall()
    {
        var user = CreateUser();
        var credential = PasswordCredential.Create("hash-1", "Argon2id", 1, DateTimeOffset.UtcNow);

        var result = user.SetPassword(credential, PolicyWithHistory(2));

        Assert.True(result.IsSuccess);
        Assert.Equal(credential, user.Credential);
        Assert.Empty(user.PasswordHistory);
    }

    [Fact]
    public void SetPassword_MovesPreviousCredentialToHistory()
    {
        var user = CreateUser();
        var first = PasswordCredential.Create("hash-1", "Argon2id", 1, DateTimeOffset.UtcNow);
        user.SetPassword(first, PolicyWithHistory(2));
        var second = PasswordCredential.Create("hash-2", "Argon2id", 1, DateTimeOffset.UtcNow);

        user.SetPassword(second, PolicyWithHistory(2));

        Assert.Equal(second, user.Credential);
        Assert.Single(user.PasswordHistory);
        Assert.Equal("hash-1", user.PasswordHistory.Single().Hash);
    }

    [Fact]
    public void SetPassword_TrimsHistoryToPolicyCount()
    {
        var user = CreateUser();
        var policy = PolicyWithHistory(1);
        user.SetPassword(PasswordCredential.Create("hash-1", "Argon2id", 1, DateTimeOffset.UtcNow), policy);
        user.SetPassword(PasswordCredential.Create("hash-2", "Argon2id", 1, DateTimeOffset.UtcNow), policy);

        user.SetPassword(PasswordCredential.Create("hash-3", "Argon2id", 1, DateTimeOffset.UtcNow), policy);

        Assert.Single(user.PasswordHistory);
        Assert.Equal("hash-2", user.PasswordHistory.Single().Hash);
    }

    [Fact]
    public void SetPassword_RaisesPasswordChangedDomainEvent()
    {
        var user = CreateUser();

        user.SetPassword(PasswordCredential.Create("hash-1", "Argon2id", 1, DateTimeOffset.UtcNow), PolicyWithHistory(2));

        Assert.Contains(user.DomainEvents, e => e is PasswordChangedDomainEvent);
    }
}
