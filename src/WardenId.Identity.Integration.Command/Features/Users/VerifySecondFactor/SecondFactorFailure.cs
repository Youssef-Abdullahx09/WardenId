using FluentResults;

namespace WardenId.Identity.Integration.Command.Features.Users.VerifySecondFactor;

/// <summary>Why a VerifySecondFactor command failed. For the caller's audit trail only.</summary>
public enum SecondFactorFailureReason
{
    InvalidCode = 1,

    /// <summary>Wrong code, and this attempt locked the account.</summary>
    LockedOut = 2,

    /// <summary>The account is locked, disabled or deleted.</summary>
    AccountInactive = 3,
    NotEnrolled = 4
}

/// <summary>
/// Identity tags the first error of a failed VerifySecondFactor with why it failed, so Protocol can audit the
/// attempt. Never show this to the user: every wrong code must still be answered the same way. The code is
/// never part of it.
/// </summary>
public static class SecondFactorFailure
{
    public const string ReasonMetadataKey = "SecondFactorFailureReason";

    public static Error Tag(Error error, SecondFactorFailureReason reason)
    {
        error.WithMetadata(ReasonMetadataKey, reason);
        return error;
    }

    /// <summary>The tagged reason, or null when no error is tagged.</summary>
    public static SecondFactorFailureReason? Read(IEnumerable<IError> errors)
    {
        foreach (var error in errors)
        {
            if (error.Metadata.TryGetValue(ReasonMetadataKey, out var reason) && reason is SecondFactorFailureReason failureReason)
                return failureReason;
        }

        return null;
    }
}
