using Microsoft.Extensions.Options;
using WardenId.Identity.Application.Features.Users.Authenticate;

namespace WardenId.Identity.Application.Tests.Features.Users.Authenticate;

public class HandlerTests
{
    private const string Password = "P@ssw0rd1";

    private static async Task<(Guid TenantId, User User, FakeUserRepository Repository, FakePasswordHasher Hasher)> SeedActiveUserAsync()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        var hasher = new FakePasswordHasher();
        user.SetPassword(PasswordCredential.Create(hasher.Hash(Password), "Argon2id", 1, DateTimeOffset.UtcNow), PasswordPolicy.FromTrustedSource(8, true, true, false, true, 5));
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        return (tenantId, user, repository, hasher);
    }

    [Fact]
    public async Task Handle_Succeeds_WithCorrectPasswordByEmail()
    {
        var (tenantId, user, repository, hasher) = await SeedActiveUserAsync();
        var handler = new Handler(repository, hasher, Options.Create(new IdentityPolicyOptions()));

        var result = await handler.Handle(new Command(tenantId, "someone@example.com", Password), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(user.Id, result.Value.UserId);
    }

    [Fact]
    public async Task Handle_Succeeds_WithCorrectPasswordByUsername()
    {
        var (tenantId, _, repository, hasher) = await SeedActiveUserAsync();
        var handler = new Handler(repository, hasher, Options.Create(new IdentityPolicyOptions()));

        var result = await handler.Handle(new Command(tenantId, "someone", Password), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_Fails_WithWrongPassword_AndRecordsFailedLogin()
    {
        var (tenantId, user, repository, hasher) = await SeedActiveUserAsync();
        var handler = new Handler(repository, hasher, Options.Create(new IdentityPolicyOptions()));

        var result = await handler.Handle(new Command(tenantId, "someone@example.com", "wrong-password"), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(1, user.Lockout.FailedAttemptCount);
    }

    [Fact]
    public async Task Handle_Fails_WithUnknownEmail_SameErrorAsWrongPassword()
    {
        var handler = new Handler(new FakeUserRepository(), new FakePasswordHasher(), Options.Create(new IdentityPolicyOptions()));

        var result = await handler.Handle(new Command(Guid.NewGuid(), "unknown@example.com", "whatever"), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(UserErrorCodes.Validation, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
    }

    [Fact]
    public async Task Handle_Locks_AfterMaxFailedAttempts()
    {
        var (tenantId, user, repository, hasher) = await SeedActiveUserAsync();
        var handler = new Handler(repository, hasher, Options.Create(new IdentityPolicyOptions { MaxFailedLoginAttempts = 1 }));

        var result = await handler.Handle(new Command(tenantId, "someone@example.com", "wrong-password"), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(UserStatus.Locked, user.Status);
        Assert.Equal(UserErrorCodes.AccountLocked, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
    }

    [Fact]
    public async Task Handle_RejectsImmediately_WhenAlreadyLockedWithFutureLockout()
    {
        var (tenantId, user, repository, hasher) = await SeedActiveUserAsync();
        user.RecordFailedLogin(1, TimeSpan.FromMinutes(15));
        var handler = new Handler(repository, hasher, Options.Create(new IdentityPolicyOptions()));

        var result = await handler.Handle(new Command(tenantId, "someone@example.com", Password), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(UserErrorCodes.AccountLocked, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
        Assert.Equal(1, user.Lockout.FailedAttemptCount);
    }
}
