using System.Security.Cryptography;
using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Application.Mfa;
using WardenId.Identity.Domain;
using WardenId.Identity.Integration.Command.Features.Users.VerifySecondFactor;
using Command = WardenId.Identity.Integration.Command.Features.Users.VerifySecondFactor.Command;

namespace WardenId.Identity.Application.Features.Users.VerifySecondFactor;

/// <summary>
/// The code step of a sign-in, after the password. It shares the failed-attempt counter with the password step:
/// a wrong code is a failed login under the tenant's lockout policy, and only a right code counts as the
/// successful login that resets the counter (the password step leaves it alone for a user with a second factor),
/// so knowing the password doesn't buy unlimited guesses at the code.
/// </summary>
public sealed class Handler(
    IUserRepository repository,
    IdentityPolicyResolver policies,
    ITotpSecretProtector secretProtector,
    TimeProvider timeProvider) : IRequestHandler<Command, Result<SecondFactorVerified>>
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

    public async Task<Result<SecondFactorVerified>> Handle(Command request, CancellationToken cancellationToken)
    {
        var policyResult = await policies.ResolveAsync(request.TenantId, cancellationToken);
        if (policyResult.IsFailed) return Result.Fail(policyResult.Errors);
        var policy = policyResult.Value;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var (result, conflicted) = await AttemptAsync(request, policy, cancellationToken);
            if (!conflicted) return result;
        }

        return Fail(UserErrors.MfaCodeInvalid(), SecondFactorFailureReason.InvalidCode);
    }

    private async Task<(Result<SecondFactorVerified> Result, bool Conflicted)> AttemptAsync(
        Command request, EffectiveIdentityPolicy policy, CancellationToken cancellationToken)
    {
        var userResult = await repository.GetByIdAsync(request.TenantId, request.UserId, cancellationToken);
        if (userResult.IsFailed)
            return (Fail(UserErrors.InvalidCredentials(), SecondFactorFailureReason.AccountInactive), false);
        var user = userResult.Value;

        var now = timeProvider.GetUtcNow();

        if (user.Status is UserStatus.Disabled or UserStatus.SoftDeleted)
            return (Fail(UserErrors.InvalidCredentials(), SecondFactorFailureReason.AccountInactive), false);

        if (user.Status == UserStatus.Locked && user.Lockout.LockedUntilUtc is { } lockedUntil && lockedUntil > now)
            return (Fail(UserErrors.AccountLocked(lockedUntil), SecondFactorFailureReason.AccountInactive), false);

        if (!user.HasEnabledMfaFactor)
            return (Fail(UserErrors.MfaNotEnrolled(), SecondFactorFailureReason.NotEnrolled), false);

        var method = TryTotp(user, request.Code, now)
            ? SecondFactorMethod.Totp
            : TryRecoveryCode(user, request.Code, now)
                ? SecondFactorMethod.RecoveryCode
                : (SecondFactorMethod?)null;

        if (method is null)
        {
            var failResult = user.RecordFailedLogin(policy.MaxFailedLoginAttempts, policy.LockoutDuration);
            if (failResult.IsFailed) return (Result.Fail(failResult.Errors), false);

            var failSave = await repository.UpdateAsync(user, user.Id, cancellationToken);
            if (failSave.IsFailed) return (default!, true);

            return (user.Status == UserStatus.Locked && user.Lockout.LockedUntilUtc is { } newLockedUntil
                ? Fail(UserErrors.AccountLocked(newLockedUntil), SecondFactorFailureReason.LockedOut)
                : Fail(UserErrors.MfaCodeInvalid(), SecondFactorFailureReason.InvalidCode), false);
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

        var saveResult = await repository.UpdateAsync(user, user.Id, cancellationToken);
        if (saveResult.IsFailed) return (default!, true);

        return (Result.Ok(new SecondFactorVerified(method.Value, user.RemainingRecoveryCodeCount)), false);
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
    private static Result<SecondFactorVerified> Fail(Error error, SecondFactorFailureReason reason) =>
        Result.Fail(SecondFactorFailure.Tag(error, reason));
}
