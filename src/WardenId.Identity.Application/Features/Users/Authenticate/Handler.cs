using FluentResults;
using MediatR;
using Microsoft.Extensions.Options;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.Authenticate;

public sealed class Handler(
    IUserRepository repository,
    IPasswordHasher passwordHasher,
    IOptions<IdentityPolicyOptions> options) : IRequestHandler<Command, Result<AuthenticatedUser>>
{
    // Not a real credential — verified against on the not-found/deactivated paths so those
    // branches cost the same hashing time as a real password check (timing side-channel).
    private const string DummyPasswordHash = "$argon2id$v=19$m=19456,t=2,p=1$AAAAAAAAAAAAAAAAAAAAAA$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    public async Task<Result<AuthenticatedUser>> Handle(Command request, CancellationToken cancellationToken)
    {
        var emailResult = Email.Create(request.EmailOrUsername);

        var userResult = emailResult.IsSuccess
            ? await repository.GetByEmailAsync(request.TenantId, emailResult.Value, cancellationToken)
            : await LookupByUsernameAsync(request, cancellationToken);

        if (userResult.IsFailed)
        {
            passwordHasher.Verify(request.Password, DummyPasswordHash);
            return Result.Fail(UserErrors.InvalidCredentials());
        }

        var user = userResult.Value;

        if (user.Status == UserStatus.Locked && user.Lockout.LockedUntilUtc is { } lockedUntil && lockedUntil > DateTimeOffset.UtcNow)
            return Result.Fail(UserErrors.AccountLocked(lockedUntil));

        // Disabled/soft-deleted accounts must never authenticate, regardless of password
        // correctness — checked before the password verify so the response (incl. timing)
        // is indistinguishable from an unknown account.
        if (user.Status is UserStatus.Disabled or UserStatus.SoftDeleted)
        {
            passwordHasher.Verify(request.Password, DummyPasswordHash);
            return Result.Fail(UserErrors.InvalidCredentials());
        }

        if (user.Credential is null || !passwordHasher.Verify(request.Password, user.Credential.Hash))
        {
            var failResult = user.RecordFailedLogin(options.Value.MaxFailedLoginAttempts, options.Value.LockoutDuration);
            if (failResult.IsFailed) return Result.Fail(failResult.Errors);

            await repository.UpdateAsync(user, user.Id, cancellationToken);

            return user.Status == UserStatus.Locked && user.Lockout.LockedUntilUtc is { } newLockedUntil
                ? Result.Fail(UserErrors.AccountLocked(newLockedUntil))
                : Result.Fail(UserErrors.InvalidCredentials());
        }

        if (user.Status == UserStatus.Locked)
        {
            var unlockResult = user.Unlock();
            if (unlockResult.IsFailed) return Result.Fail(unlockResult.Errors);
        }
        else
        {
            user.RecordSuccessfulLogin();
        }

        if (user.Credential.NeedsRehash(options.Value.CurrentPasswordAlgorithmVersion))
            user.RehashCredential(passwordHasher.Hash(request.Password), options.Value.CurrentPasswordAlgorithmVersion);

        await repository.UpdateAsync(user, user.Id, cancellationToken);

        return Result.Ok(new AuthenticatedUser(user.Id, user.Status, user.PendingActions));
    }

    private async Task<Result<User>> LookupByUsernameAsync(Command request, CancellationToken cancellationToken)
    {
        var usernameResult = Username.Create(request.EmailOrUsername);
        if (usernameResult.IsFailed) return Result.Fail<User>(UserErrors.InvalidCredentials());

        return await repository.GetByUsernameAsync(request.TenantId, usernameResult.Value, cancellationToken);
    }
}
