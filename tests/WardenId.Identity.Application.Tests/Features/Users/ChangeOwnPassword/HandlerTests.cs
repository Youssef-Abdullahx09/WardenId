using WardenId.Identity.Application.Features.Users.ChangeOwnPassword;
using WardenId.Identity.Domain.Events;
using WardenId.Identity.Integration.Command.Features.Users.Authenticate;
using Command = WardenId.Identity.Integration.Command.Features.Users.ChangeOwnPassword.Command;

namespace WardenId.Identity.Application.Tests.Features.Users.ChangeOwnPassword;

public class HandlerTests
{
    private const string CurrentPassword = "P@ssw0rd1";

    private readonly FakeUserRepository _repository = new();
    private readonly FakePasswordHasher _hasher = new();

    private Handler CreateHandler(IdentityPolicyOptions? options = null) =>
        new(_repository, _hasher, new FakeBreachedPasswordChecker(), TestPolicies.Resolver(options ?? new IdentityPolicyOptions()));

    private async Task<User> SeedUserAsync()
    {
        var user = TestUsers.CreatePendingVerification(Guid.NewGuid());
        user.SetPassword(
            PasswordCredential.Create(_hasher.Hash(CurrentPassword), "Argon2id", 1, DateTimeOffset.UtcNow),
            PasswordPolicy.FromTrustedSource(8, true, true, false, true, 5));
        user.ClearDomainEvents();
        await _repository.CreateAsync(user, Guid.NewGuid());
        return user;
    }

    [Fact]
    public async Task Handle_SetsTheNewPassword_AndRaisesPasswordChanged_WhichEndsTheSessions()
    {
        var user = await SeedUserAsync();

        var result = await CreateHandler().Handle(new Command(user.TenantId, user.Id, CurrentPassword, "N3wP@ssword"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(_hasher.Verify("N3wP@ssword", user.Credential!.Hash));
        Assert.Contains(user.DomainEvents, e => e is PasswordChangedDomainEvent);
        Assert.Equal(user.Id, _repository.LastActorId);
    }

    [Fact]
    public async Task Handle_AWrongCurrentPassword_CountsAsAFailedLogin()
    {
        var user = await SeedUserAsync();

        var result = await CreateHandler().Handle(new Command(user.TenantId, user.Id, "wrong", "N3wP@ssword"), CancellationToken.None);

        Assert.Equal(UserErrors.InvalidCredentials().Message, Assert.Single(result.Errors).Message);
        Assert.Equal(1, user.Lockout.FailedAttemptCount);
        Assert.Contains(user.DomainEvents, e => e is LoginFailedDomainEvent);
        Assert.True(_hasher.Verify(CurrentPassword, user.Credential!.Hash));
        Assert.Equal((FailureReason.InvalidPassword, (Guid?)user.Id), AuthenticationFailure.Read(result.Errors));
    }

    [Fact]
    public async Task Handle_AWrongCurrentPasswordThatLocksTheAccount_IsTaggedLockedOut_ThenLocked()
    {
        var user = await SeedUserAsync();
        var handler = CreateHandler(new IdentityPolicyOptions { MaxFailedLoginAttempts = 1 });

        var lockedOut = await handler.Handle(new Command(user.TenantId, user.Id, "wrong", "N3wP@ssword"), CancellationToken.None);
        var locked = await handler.Handle(new Command(user.TenantId, user.Id, CurrentPassword, "N3wP@ssword"), CancellationToken.None);

        Assert.Equal(FailureReason.LockedOut, AuthenticationFailure.Read(lockedOut.Errors)?.Reason);
        Assert.Equal(FailureReason.Locked, AuthenticationFailure.Read(locked.Errors)?.Reason);
        Assert.True(_hasher.Verify(CurrentPassword, user.Credential!.Hash));
    }

    [Fact]
    public async Task Handle_PolicyFailures_ComeBackAsShowableErrors()
    {
        var user = await SeedUserAsync();

        var result = await CreateHandler(new IdentityPolicyOptions { PasswordMinLength = 20 })
            .Handle(new Command(user.TenantId, user.Id, CurrentPassword, "N3wP@ssword"), CancellationToken.None);

        Assert.Equal(UserErrors.PasswordTooShort(20).Message, result.Errors[0].Message);
        Assert.Equal(0, user.Lockout.FailedAttemptCount);
        Assert.Null(AuthenticationFailure.Read(result.Errors));
    }

    [Fact]
    public async Task Handle_Fails_ForAnotherTenantsUser()
    {
        var user = await SeedUserAsync();

        var result = await CreateHandler().Handle(new Command(Guid.NewGuid(), user.Id, CurrentPassword, "N3wP@ssword"), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.True(_hasher.Verify(CurrentPassword, user.Credential!.Hash));
    }
}
