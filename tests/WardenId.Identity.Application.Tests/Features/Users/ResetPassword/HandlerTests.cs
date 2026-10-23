using WardenId.Identity.Application.Features.Users.ResetPassword;
using WardenId.Identity.Application.Utilities;

namespace WardenId.Identity.Application.Tests.Features.Users.ResetPassword;

public class HandlerTests
{
    private const string CurrentPassword = "OldP@ss1";

    private static readonly PasswordPolicy SeedPolicy = PasswordPolicy.FromTrustedSource(8, true, true, false, true, 5);

    [Fact]
    public async Task Handle_SetsNewPassword_AndClearsLockout_WhenTokenIsValid()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId, "someone@example.com");
        var hasher = new FakePasswordHasher();
        user.SetPassword(PasswordCredential.Create(hasher.Hash(CurrentPassword), "Argon2id", 1, DateTimeOffset.UtcNow), SeedPolicy);
        user.RecordFailedLogin(3, TimeSpan.FromMinutes(15));
        const string rawToken = "raw-reset-token";
        user.RequestPasswordReset(PasswordResetToken.Create(SecureTokenGenerator.Hash(rawToken), DateTimeOffset.UtcNow.AddHours(1)));
        var repository = new FakeUserRepository();
        // Seeded with a different actor so the post-Handle actor assertion proves the handler set it.
        await repository.CreateAsync(user, Guid.NewGuid(), CancellationToken.None);

        var handler = new Handler(repository, hasher, new FakeBreachedPasswordChecker(), TestPolicies.Resolver(new IdentityPolicyOptions()));
        var result = await handler.Handle(new Command(tenantId, "someone@example.com", rawToken, "N3wP@ssword"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(hasher.Verify("N3wP@ssword", user.Credential!.Hash));
        Assert.Equal(0, user.Lockout.FailedAttemptCount);
        Assert.Equal(1, repository.UpdateCallCount);
        Assert.Equal(user.Id, repository.LastActorId!.Value);
    }

    [Fact]
    public async Task Handle_Fails_WhenTokenDoesNotMatch()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId, "someone@example.com");
        user.RequestPasswordReset(PasswordResetToken.Create(SecureTokenGenerator.Hash("correct-token"), DateTimeOffset.UtcNow.AddHours(1)));
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);

        var handler = new Handler(repository, new FakePasswordHasher(), new FakeBreachedPasswordChecker(), TestPolicies.Resolver(new IdentityPolicyOptions()));
        var result = await handler.Handle(new Command(tenantId, "someone@example.com", "wrong-token", "N3wP@ssword"), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(0, repository.UpdateCallCount);
    }

    [Fact]
    public async Task Handle_Fails_WhenEmailDoesNotMatchAnyUser()
    {
        var handler = new Handler(new FakeUserRepository(), new FakePasswordHasher(), new FakeBreachedPasswordChecker(), TestPolicies.Resolver(new IdentityPolicyOptions()));

        var result = await handler.Handle(new Command(Guid.NewGuid(), "nobody@example.com", "any-token", "N3wP@ssword"), CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    // Regression: the handler used to evaluate the candidate password against policy BEFORE checking the
    // reset token, turning this unauthenticated endpoint into a password oracle — a `PasswordReused`
    // response told an anonymous caller their guess matched the account's real password.
    [Fact]
    public async Task Handle_DoesNotLeakPasswordReuse_WhenTokenIsInvalid()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId, "someone@example.com");
        var hasher = new FakePasswordHasher();
        user.SetPassword(PasswordCredential.Create(hasher.Hash(CurrentPassword), "Argon2id", 1, DateTimeOffset.UtcNow), SeedPolicy);
        user.RequestPasswordReset(PasswordResetToken.Create(SecureTokenGenerator.Hash("correct-token"), DateTimeOffset.UtcNow.AddHours(1)));
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);

        var handler = new Handler(repository, hasher, new FakeBreachedPasswordChecker(), TestPolicies.Resolver(new IdentityPolicyOptions()));

        // Garbage token + a password that happens to be the user's CURRENT password.
        var result = await handler.Handle(
            new Command(tenantId, "someone@example.com", "garbage-token", CurrentPassword),
            CancellationToken.None);

        Assert.True(result.IsFailed);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UserErrors.TokenInvalid().Message, error.Message);
        Assert.NotEqual(UserErrors.PasswordReused().Message, error.Message);
        Assert.Equal(0, repository.UpdateCallCount);
    }

    // Regression: reordering the token check ahead of policy evaluation must not weaken policy enforcement.
    [Fact]
    public async Task Handle_StillEnforcesPasswordPolicy_WhenTokenIsValid()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId, "someone@example.com");
        var hasher = new FakePasswordHasher();
        user.SetPassword(PasswordCredential.Create(hasher.Hash(CurrentPassword), "Argon2id", 1, DateTimeOffset.UtcNow), SeedPolicy);
        const string rawToken = "raw-reset-token";
        user.RequestPasswordReset(PasswordResetToken.Create(SecureTokenGenerator.Hash(rawToken), DateTimeOffset.UtcNow.AddHours(1)));
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);

        var handler = new Handler(repository, hasher, new FakeBreachedPasswordChecker(), TestPolicies.Resolver(new IdentityPolicyOptions()));

        var reusedResult = await handler.Handle(
            new Command(tenantId, "someone@example.com", rawToken, CurrentPassword),
            CancellationToken.None);

        Assert.True(reusedResult.IsFailed);
        Assert.Equal(UserErrors.PasswordReused().Message, Assert.Single(reusedResult.Errors).Message);

        var weakResult = await handler.Handle(
            new Command(tenantId, "someone@example.com", rawToken, "short"),
            CancellationToken.None);

        Assert.True(weakResult.IsFailed);
        Assert.Equal(UserErrors.PasswordTooShort(8).Message, Assert.Single(weakResult.Errors).Message);

        // Neither policy failure consumed the token or persisted anything.
        Assert.Equal(0, repository.UpdateCallCount);
        Assert.Null(user.PasswordResetTokens.Single().ConsumedAtUtc);
    }
}
