using WardenId.Identity.Domain.Events;

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

    [Fact]
    public void SetPassword_FailsWhenUserIsSoftDeleted()
    {
        var user = CreateUser();
        user.SoftDelete();

        var result = user.SetPassword(PasswordCredential.Create("hash-1", "Argon2id", 1, DateTimeOffset.UtcNow), PolicyWithHistory(2));

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void SetPassword_FailsWhenUserIsDisabled()
    {
        var user = CreateUser();
        user.Disable("policy violation");

        var result = user.SetPassword(PasswordCredential.Create("hash-1", "Argon2id", 1, DateTimeOffset.UtcNow), PolicyWithHistory(2));

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void RehashCredential_ReplacesHashAndVersion()
    {
        var user = CreateUser();
        user.SetPassword(PasswordCredential.Create("hash-1", "Argon2id", 1, DateTimeOffset.UtcNow), PolicyWithHistory(2));

        var result = user.RehashCredential("hash-1-rehashed", 2);

        Assert.True(result.IsSuccess);
        Assert.Equal("hash-1-rehashed", user.Credential!.Hash);
        Assert.Equal(2, user.Credential!.AlgorithmVersion);
    }

    [Fact]
    public void RehashCredential_FailsWhenNoCredentialSet()
    {
        var user = CreateUser();

        var result = user.RehashCredential("hash-1-rehashed", 2);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void SetPassword_ClearsPendingResetPasswordAction()
    {
        var user = CreateUser();
        user.SetPassword(PasswordCredential.Create("hash-1", "Argon2id", 1, DateTimeOffset.UtcNow), PolicyWithHistory(2));
        user.AddRequiredAction(RequiredAction.ResetPassword);

        var result = user.SetPassword(PasswordCredential.Create("hash-2", "Argon2id", 1, DateTimeOffset.UtcNow), PolicyWithHistory(2));

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(RequiredAction.ResetPassword, user.PendingActions);
    }

    [Fact]
    public void SetPassword_KeepsOtherPendingActions()
    {
        var user = CreateUser();
        user.AddRequiredAction(RequiredAction.ResetPassword);
        user.AddRequiredAction(RequiredAction.AcceptTos);

        user.SetPassword(PasswordCredential.Create("hash-1", "Argon2id", 1, DateTimeOffset.UtcNow), PolicyWithHistory(2));

        Assert.Contains(RequiredAction.VerifyEmail, user.PendingActions);
        Assert.Contains(RequiredAction.AcceptTos, user.PendingActions);
    }

    [Fact]
    public void SetPassword_KeepsResetPasswordAction_WhenItFails()
    {
        var user = CreateUser();
        user.AddRequiredAction(RequiredAction.ResetPassword);
        user.Disable("policy violation");

        var result = user.SetPassword(PasswordCredential.Create("hash-1", "Argon2id", 1, DateTimeOffset.UtcNow), PolicyWithHistory(2));

        Assert.True(result.IsFailed);
        Assert.Contains(RequiredAction.ResetPassword, user.PendingActions);
    }
}
