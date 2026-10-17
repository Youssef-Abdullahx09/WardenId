using FluentResults;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;
using WardenId.Identity.Integration.Command.Features.Users.Authenticate;
using UserStatus = WardenId.Identity.Domain.UserStatus;

namespace WardenId.Identity.Application.Features.Users;

/// <summary>
/// Changes a password after checking the current one: shared by the admin API's change-password and the user's own
/// account page. A wrong current password is a failed login under the tenant's lockout policy, and a locked account
/// can't change its password until the lockout ends, so this is no way around the sign-in's guess limit. A right
/// one leaves the failed-attempt counter alone: for a user with a second factor, only a code resets it.
/// A refusal of the current password (wrong, or the account locked, disabled or deleted) is tagged with
/// <see cref="AuthenticationFailure"/>, like a sign-in's, for the caller's audit trail; policy refusals of the new
/// password are not tagged.
/// </summary>
internal sealed class PasswordChange(
    IUserRepository repository,
    IPasswordHasher passwordHasher,
    IBreachedPasswordChecker breachedPasswordChecker,
    IdentityPolicyResolver policies)
{
    /// <summary>
    /// How many times a wrong current password is counted when its save loses a race with another change to the user.
    /// Each try reloads the user, so the failure is counted on the current count. A right password isn't retried: its
    /// save fails with <see cref="UserErrors.ConcurrencyConflict"/>.
    /// </summary>
    internal const int MaxAttempts = 3;

    public async Task<Result> ChangeAsync(
        Guid tenantId, Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        var policyResult = await policies.ResolveAsync(tenantId, cancellationToken);
        if (policyResult.IsFailed) return Result.Fail(policyResult.Errors);
        var policy = policyResult.Value;

        for (var attempt = 1; ; attempt++)
        {
            var (result, conflicted) = await AttemptAsync(tenantId, userId, currentPassword, newPassword, policy, cancellationToken);
            if (!conflicted || attempt == MaxAttempts) return result;
        }
    }

    private async Task<(Result Result, bool Conflicted)> AttemptAsync(
        Guid tenantId,
        Guid userId,
        string currentPassword,
        string newPassword,
        EffectiveIdentityPolicy policy,
        CancellationToken cancellationToken)
    {
        var userResult = await repository.GetByIdAsync(tenantId, userId, cancellationToken);
        if (userResult.IsFailed) return (Result.Fail(userResult.Errors), false);
        var user = userResult.Value;

        if (user.Status == UserStatus.Locked && user.Lockout.LockedUntilUtc is { } lockedUntil && lockedUntil > DateTimeOffset.UtcNow)
            return (Fail(UserErrors.AccountLocked(lockedUntil), FailureReason.Locked, user), false);

        if (user.Credential is null || !passwordHasher.Verify(currentPassword, user.Credential.Hash))
        {
            // A disabled or deleted user can't record a failure (or change a password): just refused.
            if (user.RecordFailedLogin(policy.MaxFailedLoginAttempts, policy.LockoutDuration).IsFailed)
                return (Fail(UserErrors.InvalidCredentials(), InactiveReason(user), user), false);

            var failSave = await repository.UpdateAsync(user, user.Id, cancellationToken);
            if (failSave.IsFailed) return (Fail(UserErrors.InvalidCredentials(), FailureReason.InvalidPassword, user), true);

            return (user.Status == UserStatus.Locked && user.Lockout.LockedUntilUtc is { } newLockedUntil
                ? Fail(UserErrors.AccountLocked(newLockedUntil), FailureReason.LockedOut, user)
                : Fail(UserErrors.InvalidCredentials(), FailureReason.InvalidPassword, user), false);
        }

        var passwordPolicy = policy.Password;
        var evaluateResult = await PasswordPolicyEvaluator.EvaluateAsync(
            passwordPolicy, newPassword, user.Credential, user.PasswordHistory, passwordHasher, breachedPasswordChecker);
        if (evaluateResult.IsFailed) return (evaluateResult, false);

        var credential = PasswordCredential.Create(
            passwordHasher.Hash(newPassword), passwordHasher.Algorithm, policy.CurrentPasswordAlgorithmVersion, DateTimeOffset.UtcNow);

        var setPasswordResult = user.SetPassword(credential, passwordPolicy);
        if (setPasswordResult.IsFailed) return (setPasswordResult, false);

        var saveResult = await repository.UpdateAsync(user, user.Id, cancellationToken);
        return (saveResult.IsFailed ? Result.Fail(saveResult.Errors) : Result.Ok(), false);
    }

    private static FailureReason InactiveReason(User user) =>
        user.Status == UserStatus.SoftDeleted ? FailureReason.Deleted : FailureReason.Disabled;

    private static Result Fail(Error error, FailureReason reason, User user) =>
        Result.Fail(AuthenticationFailure.Tag(error, reason, user.Id));
}
