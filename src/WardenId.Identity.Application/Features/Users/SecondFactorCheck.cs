using System.Security.Cryptography;
using FluentResults;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Application.Mfa;
using WardenId.Identity.Domain;
using WardenId.Identity.Integration.Command.Features.Users.VerifySecondFactor;

namespace WardenId.Identity.Application.Features.Users;

/// <summary>
/// Checks a code of the user's second factor: the code step of a sign-in (VerifySecondFactor) and the step-up in
/// front of every MFA change on the account page. It shares the failed-attempt counter with the password step: a
/// wrong code is a failed login under the tenant's lockout policy, and only a right code counts as the successful
/// login that resets the counter, so knowing the password doesn't buy unlimited guesses at the code. A TOTP code
/// is accepted once (replay), and a recovery code is used up. Failures are tagged with
/// <see cref="SecondFactorFailure"/> for the caller's audit trail.
/// </summary>
internal sealed class SecondFactorCheck(
    IUserRepository repository,
    ITotpSecretProtector secretProtector,
    TimeProvider timeProvider)
{
    /// <summary>
    /// How many times a code is tried when its save loses a race with another change to the user, a factor or a
    /// recovery code (see <see cref="IUserRepository.UpdateAsync"/>). Each try reloads the user and checks the code
    /// again, so two requests can't both accept one code (the second sees it used and fails like a wrong code), and
    /// a wrong code is counted on the current failed-attempt count. Up to this many requests racing on one user all
    /// get their save in; beyond that, a request out of tries is refused like a wrong code, uncounted — it was
    /// accepted for nobody, so it tells the caller nothing.
    /// </summary>
    internal const int MaxAttempts = 5;

    /// <summary>
    /// Checks <paramref name="code"/>; when it's right, applies <paramref name="onVerified"/> to the user and saves
    /// both together, so the change and the code's use stand or fall as one. <paramref name="onVerified"/> runs again
    /// on each retry, on the reloaded user. When it fails, nothing is saved (the code isn't used up either) and its
    /// errors come back untagged.
    /// </summary>
    /// <param name="allowRecoveryCode">Whether a recovery code may stand in for a TOTP code.</param>
    public async Task<Result<T>> RunAsync<T>(
        Guid tenantId,
        Guid userId,
        string code,
        EffectiveIdentityPolicy policy,
        bool allowRecoveryCode,
        Func<User, SecondFactorMethod, Result<T>> onVerified,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var (result, conflicted) = await AttemptAsync(tenantId, userId, code, policy, allowRecoveryCode, onVerified, cancellationToken);
            if (!conflicted) return result;
        }

        return Fail<T>(UserErrors.MfaCodeInvalid(), SecondFactorFailureReason.InvalidCode);
    }

    private async Task<(Result<T> Result, bool Conflicted)> AttemptAsync<T>(
        Guid tenantId,
        Guid userId,
        string code,
        EffectiveIdentityPolicy policy,
        bool allowRecoveryCode,
        Func<User, SecondFactorMethod, Result<T>> onVerified,
        CancellationToken cancellationToken)
    {
        var userResult = await repository.GetByIdAsync(tenantId, userId, cancellationToken);
        if (userResult.IsFailed)
            return (Fail<T>(UserErrors.InvalidCredentials(), SecondFactorFailureReason.AccountInactive), false);
        var user = userResult.Value;

        var now = timeProvider.GetUtcNow();

        if (user.Status is UserStatus.Disabled or UserStatus.SoftDeleted)
            return (Fail<T>(UserErrors.InvalidCredentials(), SecondFactorFailureReason.AccountInactive), false);

        if (user.Status == UserStatus.Locked && user.Lockout.LockedUntilUtc is { } lockedUntil && lockedUntil > now)
            return (Fail<T>(UserErrors.AccountLocked(lockedUntil), SecondFactorFailureReason.AccountInactive), false);

        if (!user.HasEnabledMfaFactor)
            return (Fail<T>(UserErrors.MfaNotEnrolled(), SecondFactorFailureReason.NotEnrolled), false);

        var method = TryTotp(user, code, now)
            ? SecondFactorMethod.Totp
            : allowRecoveryCode && TryRecoveryCode(user, code, now)
                ? SecondFactorMethod.RecoveryCode
                : (SecondFactorMethod?)null;

        if (method is null)
        {
            var failResult = user.RecordFailedLogin(policy.MaxFailedLoginAttempts, policy.LockoutDuration);
            if (failResult.IsFailed) return (Result.Fail(failResult.Errors), false);

            var failSave = await repository.UpdateAsync(user, user.Id, cancellationToken);
            if (failSave.IsFailed) return (default!, true);

            return (user.Status == UserStatus.Locked && user.Lockout.LockedUntilUtc is { } newLockedUntil
                ? Fail<T>(UserErrors.AccountLocked(newLockedUntil), SecondFactorFailureReason.LockedOut)
                : Fail<T>(UserErrors.MfaCodeInvalid(), SecondFactorFailureReason.InvalidCode), false);
        }

        if (user.Status == UserStatus.Locked)
        {
            var unlockResult = user.Unlock();
            if (unlockResult.IsFailed) return (Result.Fail(unlockResult.Errors), false);
        }
        else
        {
            user.RecordSuccessfulLogin();
        }

        var changeResult = onVerified(user, method.Value);
        if (changeResult.IsFailed) return (changeResult, false);

        var saveResult = await repository.UpdateAsync(user, user.Id, cancellationToken);
        if (saveResult.IsFailed) return (default!, true);

        return (changeResult, false);
    }

    /// <summary>
    /// Accepts the code for the oldest matching time step newer than the factor's last accepted one: a code already
    /// used, or older than the last one used, is refused like a wrong code.
    /// </summary>
    private bool TryTotp(User user, string code, DateTimeOffset now)
    {
        if (Totp.NormalizeCode(code) is null) return false;

        foreach (var factor in user.MfaFactors.Where(f => f.Enabled && f.FactorType == MfaFactorType.Totp))
        {
            // A factor whose secret can't be read never matches; an admin reset replaces it.
            var secret = secretProtector.TryUnprotect(factor.CredentialData);
            if (secret is null) continue;

            IReadOnlyList<long> steps;
            try
            {
                steps = Totp.MatchingTimeSteps(secret, code, now);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(secret);
            }

            foreach (var step in steps.Where(factor.CanAcceptTimeStep))
            {
                if (user.AcceptMfaCode(factor.Id, step, now).IsSuccess) return true;
            }
        }

        return false;
    }

    private static bool TryRecoveryCode(User user, string code, DateTimeOffset now)
    {
        var normalized = RecoveryCodes.Normalize(code);
        if (normalized is null) return false;

        return user.ConsumeRecoveryCode(RecoveryCodes.Hash(normalized), now).IsSuccess;
    }

    // The tag is for the caller's audit trail; the error itself (message, code) is what a user may be told.
    private static Result<T> Fail<T>(Error error, SecondFactorFailureReason reason) =>
        Result.Fail(SecondFactorFailure.Tag(error, reason));
}
