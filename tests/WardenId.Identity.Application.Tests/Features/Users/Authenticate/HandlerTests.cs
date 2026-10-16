using Microsoft.Extensions.Options;
using WardenId.Identity.Application.Features.Users.Authenticate;
using WardenId.Identity.Integration.Command.Features.Users.Authenticate;
using Command = WardenId.Identity.Integration.Command.Features.Users.Authenticate.Command;
using UserStatus = WardenId.Identity.Domain.UserStatus;

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
        Assert.Equal(UserErrorCodes.InvalidCredentials, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
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

    [Fact]
    public async Task Handle_UnlocksUser_WhenLockoutWindowHasAlreadyExpired()
    {
        var (tenantId, user, repository, hasher) = await SeedActiveUserAsync();
        user.RecordFailedLogin(1, TimeSpan.FromMinutes(-1));
        var handler = new Handler(repository, hasher, Options.Create(new IdentityPolicyOptions()));

        var result = await handler.Handle(new Command(tenantId, "someone@example.com", Password), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal(0, user.Lockout.FailedAttemptCount);
        Assert.Null(user.Lockout.LockedUntilUtc);
    }

    [Fact]
    public async Task Handle_RehashesCredential_WhenAlgorithmVersionIsStale()
    {
        var (tenantId, user, repository, hasher) = await SeedActiveUserAsync();
        var handler = new Handler(repository, hasher, Options.Create(new IdentityPolicyOptions { CurrentPasswordAlgorithmVersion = 2 }));

        var result = await handler.Handle(new Command(tenantId, "someone@example.com", Password), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, user.Credential!.AlgorithmVersion);
        Assert.True(hasher.Verify(Password, user.Credential.Hash));
    }

    [Fact]
    public async Task Handle_Fails_WhenUserIsDisabled()
    {
        var (tenantId, user, repository, hasher) = await SeedActiveUserAsync();
        user.Disable("reason");
        var handler = new Handler(repository, hasher, Options.Create(new IdentityPolicyOptions()));

        var result = await handler.Handle(new Command(tenantId, "someone@example.com", Password), CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task Handle_Fails_WhenUserIsSoftDeleted()
    {
        var (tenantId, user, repository, hasher) = await SeedActiveUserAsync();
        user.SoftDelete();
        var handler = new Handler(repository, hasher, Options.Create(new IdentityPolicyOptions()));

        var result = await handler.Handle(new Command(tenantId, "someone@example.com", Password), CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    // The failure detail is for Protocol's audit trail only; the error the caller shows stays the same.
    private static (FailureReason Reason, Guid? UserId) FailureOf(IResultBase result)
    {
        Assert.True(result.IsFailed);
        var failure = AuthenticationFailure.Read(result.Errors);
        Assert.NotNull(failure);
        return failure.Value;
    }

    [Fact]
    public async Task Failure_UnknownAccount_IsTaggedUnknownUser_WithoutUserId()
    {
        var handler = new Handler(new FakeUserRepository(), new FakePasswordHasher(), Options.Create(new IdentityPolicyOptions()));

        var result = await handler.Handle(new Command(Guid.NewGuid(), "unknown@example.com", "whatever"), CancellationToken.None);

        Assert.Equal((FailureReason.UnknownUser, (Guid?)null), FailureOf(result));
        Assert.Equal(UserErrorCodes.InvalidCredentials, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
    }

    [Fact]
    public async Task Failure_InvalidUsernameFormat_IsTaggedUnknownUser()
    {
        var handler = new Handler(new FakeUserRepository(), new FakePasswordHasher(), Options.Create(new IdentityPolicyOptions()));

        var result = await handler.Handle(new Command(Guid.NewGuid(), "!", "whatever"), CancellationToken.None);

        Assert.Equal((FailureReason.UnknownUser, (Guid?)null), FailureOf(result));
    }

    [Fact]
    public async Task Failure_WrongPassword_IsTaggedInvalidPassword_WithUserId()
    {
        var (tenantId, user, repository, hasher) = await SeedActiveUserAsync();
        var handler = new Handler(repository, hasher, Options.Create(new IdentityPolicyOptions()));

        var result = await handler.Handle(new Command(tenantId, "someone@example.com", "wrong-password"), CancellationToken.None);

        Assert.Equal((FailureReason.InvalidPassword, (Guid?)user.Id), FailureOf(result));
        Assert.Equal(UserErrorCodes.InvalidCredentials, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
    }

    [Fact]
    public async Task Failure_WrongPasswordThatLocksTheAccount_IsTaggedLockedOut()
    {
        var (tenantId, user, repository, hasher) = await SeedActiveUserAsync();
        var handler = new Handler(repository, hasher, Options.Create(new IdentityPolicyOptions { MaxFailedLoginAttempts = 1 }));

        var result = await handler.Handle(new Command(tenantId, "someone@example.com", "wrong-password"), CancellationToken.None);

        Assert.Equal((FailureReason.LockedOut, (Guid?)user.Id), FailureOf(result));
        Assert.Equal(UserErrorCodes.AccountLocked, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
    }

    [Fact]
    public async Task Failure_AlreadyLocked_IsTaggedLocked()
    {
        var (tenantId, user, repository, hasher) = await SeedActiveUserAsync();
        user.RecordFailedLogin(1, TimeSpan.FromMinutes(15));
        var handler = new Handler(repository, hasher, Options.Create(new IdentityPolicyOptions()));

        var result = await handler.Handle(new Command(tenantId, "someone@example.com", Password), CancellationToken.None);

        Assert.Equal((FailureReason.Locked, (Guid?)user.Id), FailureOf(result));
    }

    [Fact]
    public async Task Failure_Disabled_IsTaggedDisabled()
    {
        var (tenantId, user, repository, hasher) = await SeedActiveUserAsync();
        user.Disable("reason");
        var handler = new Handler(repository, hasher, Options.Create(new IdentityPolicyOptions()));

        var result = await handler.Handle(new Command(tenantId, "someone@example.com", Password), CancellationToken.None);

        Assert.Equal((FailureReason.Disabled, (Guid?)user.Id), FailureOf(result));
    }

    [Fact]
    public async Task Failure_SoftDeleted_IsTaggedDeleted()
    {
        var (tenantId, user, repository, hasher) = await SeedActiveUserAsync();
        user.SoftDelete();
        var handler = new Handler(repository, hasher, Options.Create(new IdentityPolicyOptions()));

        var result = await handler.Handle(new Command(tenantId, "someone@example.com", Password), CancellationToken.None);

        Assert.Equal((FailureReason.Deleted, (Guid?)user.Id), FailureOf(result));
    }

    [Fact]
    public void Read_UntaggedErrors_IsNull()
    {
        Assert.Null(AuthenticationFailure.Read([new Error("boom")]));
    }
}
