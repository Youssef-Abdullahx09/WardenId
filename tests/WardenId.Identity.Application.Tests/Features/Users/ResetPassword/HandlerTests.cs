using Microsoft.Extensions.Options;
using WardenId.Identity.Application.Features.Users.ResetPassword;

namespace WardenId.Identity.Application.Tests.Features.Users.ResetPassword;

public class HandlerTests
{
    [Fact]
    public async Task Handle_SetsNewPassword_AndClearsLockout_WhenTokenIsValid()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId, "someone@example.com");
        var hasher = new FakePasswordHasher();
        user.SetPassword(PasswordCredential.Create(hasher.Hash("OldP@ss1"), "Argon2id", 1, DateTimeOffset.UtcNow), PasswordPolicy.FromTrustedSource(8, true, true, false, true, 5));
        user.RecordFailedLogin(3, TimeSpan.FromMinutes(15));
        const string rawToken = "raw-reset-token";
        user.RequestPasswordReset(PasswordResetToken.Create(SecureTokenGenerator.Hash(rawToken), DateTimeOffset.UtcNow.AddHours(1)));
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);

        var handler = new Handler(repository, hasher, new FakeBreachedPasswordChecker(), Options.Create(new IdentityPolicyOptions()));
        var result = await handler.Handle(new Command(tenantId, "someone@example.com", rawToken, "N3wP@ssword"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(hasher.Verify("N3wP@ssword", user.Credential!.Hash));
        Assert.Equal(0, user.Lockout.FailedAttemptCount);
    }

    [Fact]
    public async Task Handle_Fails_WhenTokenDoesNotMatch()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId, "someone@example.com");
        user.RequestPasswordReset(PasswordResetToken.Create(SecureTokenGenerator.Hash("correct-token"), DateTimeOffset.UtcNow.AddHours(1)));
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);

        var handler = new Handler(repository, new FakePasswordHasher(), new FakeBreachedPasswordChecker(), Options.Create(new IdentityPolicyOptions()));
        var result = await handler.Handle(new Command(tenantId, "someone@example.com", "wrong-token", "N3wP@ssword"), CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task Handle_Fails_WhenEmailDoesNotMatchAnyUser()
    {
        var handler = new Handler(new FakeUserRepository(), new FakePasswordHasher(), new FakeBreachedPasswordChecker(), Options.Create(new IdentityPolicyOptions()));

        var result = await handler.Handle(new Command(Guid.NewGuid(), "nobody@example.com", "any-token", "N3wP@ssword"), CancellationToken.None);

        Assert.True(result.IsFailed);
    }
}
