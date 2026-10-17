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

    /// <summary>
    /// How many times a sign-in is tried when its save loses a race with another change to the user (see
    /// <see cref="IUserRepository.UpdateAsync"/>). Each try reloads the user and checks everything again, so a
    /// wrong password is counted on the current failed-attempt count rather than a stale one.
    /// </summary>
    internal const int MaxAttempts = 3;

    public async Task<Result<AuthenticatedUser>> Handle(Command request, CancellationToken cancellationToken)
    {
        // Resolved up front on every path, so a known account costs no extra lookup over an unknown one.
        var policyResult = await policies.ResolveAsync(request.TenantId, cancellationToken);
        if (policyResult.IsFailed) return Result.Fail(policyResult.Errors);
        var policy = policyResult.Value;

        for (var attempt = 1; ; attempt++)
        {
            var (result, conflicted) = await AttemptAsync(request, policy, cancellationToken);
            if (!conflicted || attempt == MaxAttempts) return result;
        }
    }

    /// <summary>
    /// One try. <c>Conflicted</c> is true when its save lost a race; <c>Result</c> is then what to answer if there's
    /// no try left: a wrong password stays a wrong password (refused, though maybe not counted), and a right one
    /// fails untagged with <see cref="UserErrors.ConcurrencyConflict"/>, since nothing of the sign-in was saved.
    /// </summary>
    private async Task<(Result<AuthenticatedUser> Result, bool Conflicted)> AttemptAsync(
        Command request, EffectiveIdentityPolicy policy, CancellationToken cancellationToken)
    {
        var emailResult = Email.Create(request.EmailOrUsername);

        var userResult = emailResult.IsSuccess
            ? await repository.GetByEmailAsync(request.TenantId, emailResult.Value, cancellationToken)
            : await LookupByUsernameAsync(request, cancellationToken);

        if (userResult.IsFailed)
        {
            passwordHasher.Verify(request.Password, DummyPasswordHash);
            return (Fail(UserErrors.InvalidCredentials(), FailureReason.UnknownUser, null), false);
        }

        var user = userResult.Value;

        if (user.Status == UserStatus.Locked && user.Lockout.LockedUntilUtc is { } lockedUntil && lockedUntil > DateTimeOffset.UtcNow)
            return (Fail(UserErrors.AccountLocked(lockedUntil), FailureReason.Locked, user.Id), false);

        // Disabled/soft-deleted accounts must never authenticate, regardless of password
        // correctness — checked before the password verify so the response (incl. timing)
        // is indistinguishable from an unknown account.
        if (user.Status is UserStatus.Disabled or UserStatus.SoftDeleted)
        {
            passwordHasher.Verify(request.Password, DummyPasswordHash);
            return (Fail(
                UserErrors.InvalidCredentials(),
                user.Status == UserStatus.Disabled ? FailureReason.Disabled : FailureReason.Deleted,
                user.Id), false);
        }

        if (user.Credential is null || !passwordHasher.Verify(request.Password, user.Credential.Hash))
        {
            var failResult = user.RecordFailedLogin(policy.MaxFailedLoginAttempts, policy.LockoutDuration);
            if (failResult.IsFailed) return (Result.Fail(failResult.Errors), false);

            var failSave = await repository.UpdateAsync(user, user.Id, cancellationToken);

            var failure = user.Status == UserStatus.Locked && user.Lockout.LockedUntilUtc is { } newLockedUntil
                ? Fail(UserErrors.AccountLocked(newLockedUntil), FailureReason.LockedOut, user.Id)
                : Fail(UserErrors.InvalidCredentials(), FailureReason.InvalidPassword, user.Id);
            return failSave.IsFailed
                ? (Fail(UserErrors.InvalidCredentials(), FailureReason.InvalidPassword, user.Id), true)
                : (failure, false);
        }

        // A lockout that has run out ends here either way. Otherwise the failed-attempt counter is reset only
        // when the password completes the sign-in: with a second factor, the code step (VerifySecondFactor)
        // counts as the successful login, so a known password doesn't reset the count of wrong codes.
        var hasSecondFactor = user.HasEnabledMfaFactor;
        if (user.Status == UserStatus.Locked)
        {
            var unlockResult = user.Unlock();
            if (unlockResult.IsFailed) return (Result.Fail(unlockResult.Errors), false);
        }
        else if (!hasSecondFactor)
        {
            user.RecordSuccessfulLogin();
        }

        if (user.Credential.NeedsRehash(policy.CurrentPasswordAlgorithmVersion))
            user.RehashCredential(passwordHasher.Hash(request.Password), policy.CurrentPasswordAlgorithmVersion);

        var saveResult = await repository.UpdateAsync(user, user.Id, cancellationToken);
        if (saveResult.IsFailed) return (Result.Fail(saveResult.Errors), true);

        return (Result.Ok(new AuthenticatedUser(
            user.Id,
            user.Email.Value,
            user.Username.Value,
            ContractMapping.MapStatus(user.Status),
            EffectivePendingActions(user, policy.Mfa).Select(ContractMapping.MapRequiredAction).ToArray(),
            hasSecondFactor)), false);
    }

    // EnrollMfa is added when the policy requires MFA of a user with no enabled factor, without storing it: the
    // policy can change, and then the requirement goes with it.
    private static IEnumerable<RequiredAction> EffectivePendingActions(User user, MfaPolicy policy) =>
        MfaRequirement.MustEnroll(user, policy)
            ? user.PendingActions.Append(RequiredAction.EnrollMfa).Distinct()
            : user.PendingActions;

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
