using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;
using AuthenticatedUser = WardenId.Identity.Integration.Command.Features.Users.Authenticate.AuthenticatedUser;
using AuthenticationFailure = WardenId.Identity.Integration.Command.Features.Users.Authenticate.AuthenticationFailure;
using FailureReason = WardenId.Identity.Integration.Command.Features.Users.Authenticate.FailureReason;
using Command = WardenId.Identity.Integration.Command.Features.Users.Authenticate.Command;

namespace WardenId.Identity.Application.Features.Users.Authenticate;

public sealed class Handler(
    IUserRepository repository,
    IPasswordHasher passwordHasher,
    IdentityPolicyResolver policies) : IRequestHandler<Command, Result<AuthenticatedUser>>
{
    // Not a real credential — verified against on the not-found/deactivated paths so those
    // branches cost the same hashing time as a real password check (timing side-channel).
    private const string DummyPasswordHash = "$argon2id$v=19$m=19456,t=2,p=1$AAAAAAAAAAAAAAAAAAAAAA$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    public async Task<Result<AuthenticatedUser>> Handle(Command request, CancellationToken cancellationToken)
    {
        // Resolved up front on every path, so a known account costs no extra lookup over an unknown one.
        var policyResult = await policies.ResolveAsync(request.TenantId, cancellationToken);
        if (policyResult.IsFailed) return Result.Fail(policyResult.Errors);
        var policy = policyResult.Value;

        var emailResult = Email.Create(request.EmailOrUsername);

        var userResult = emailResult.IsSuccess
            ? await repository.GetByEmailAsync(request.TenantId, emailResult.Value, cancellationToken)
            : await LookupByUsernameAsync(request, cancellationToken);

        if (userResult.IsFailed)
        {
            passwordHasher.Verify(request.Password, DummyPasswordHash);
            return Fail(UserErrors.InvalidCredentials(), FailureReason.UnknownUser, null);
        }

        var user = userResult.Value;

        if (user.Status == UserStatus.Locked && user.Lockout.LockedUntilUtc is { } lockedUntil && lockedUntil > DateTimeOffset.UtcNow)
            return Fail(UserErrors.AccountLocked(lockedUntil), FailureReason.Locked, user.Id);

        // Disabled/soft-deleted accounts must never authenticate, regardless of password
        // correctness — checked before the password verify so the response (incl. timing)
        // is indistinguishable from an unknown account.
        if (user.Status is UserStatus.Disabled or UserStatus.SoftDeleted)
        {
            passwordHasher.Verify(request.Password, DummyPasswordHash);
            return Fail(
                UserErrors.InvalidCredentials(),
                user.Status == UserStatus.Disabled ? FailureReason.Disabled : FailureReason.Deleted,
                user.Id);
        }

        if (user.Credential is null || !passwordHasher.Verify(request.Password, user.Credential.Hash))
        {
            var failResult = user.RecordFailedLogin(policy.MaxFailedLoginAttempts, policy.LockoutDuration);
            if (failResult.IsFailed) return Result.Fail(failResult.Errors);

            await repository.UpdateAsync(user, user.Id, cancellationToken);

            return user.Status == UserStatus.Locked && user.Lockout.LockedUntilUtc is { } newLockedUntil
                ? Fail(UserErrors.AccountLocked(newLockedUntil), FailureReason.LockedOut, user.Id)
                : Fail(UserErrors.InvalidCredentials(), FailureReason.InvalidPassword, user.Id);
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

        if (user.Credential.NeedsRehash(policy.CurrentPasswordAlgorithmVersion))
            user.RehashCredential(passwordHasher.Hash(request.Password), policy.CurrentPasswordAlgorithmVersion);

        await repository.UpdateAsync(user, user.Id, cancellationToken);

        return Result.Ok(new AuthenticatedUser(
            user.Id,
            user.Email.Value,
            user.Username.Value,
            ContractMapping.MapStatus(user.Status),
            user.PendingActions.Select(ContractMapping.MapRequiredAction).ToArray()));
    }

    // The tag is for the caller's audit trail; the error itself (message, code) is unchanged.
    private static Result<AuthenticatedUser> Fail(Error error, FailureReason reason, Guid? userId) =>
        Result.Fail(AuthenticationFailure.Tag(error, reason, userId));

    private async Task<Result<User>> LookupByUsernameAsync(Command request, CancellationToken cancellationToken)
    {
        var usernameResult = Username.Create(request.EmailOrUsername);
        if (usernameResult.IsFailed) return Result.Fail<User>(UserErrors.InvalidCredentials());

        return await repository.GetByUsernameAsync(request.TenantId, usernameResult.Value, cancellationToken);
    }
}
