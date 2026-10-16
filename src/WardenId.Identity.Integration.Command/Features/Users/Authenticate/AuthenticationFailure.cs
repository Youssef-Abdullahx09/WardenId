using FluentResults;

namespace WardenId.Identity.Integration.Command.Features.Users.Authenticate;

/// <summary>Why an Authenticate command failed. For the caller's audit trail only.</summary>
public enum FailureReason
{
    UnknownUser = 1,
    InvalidPassword = 2,

    /// <summary>Wrong password, and this attempt locked the account.</summary>
    LockedOut = 3,

    /// <summary>The account was already locked; the password was not checked.</summary>
    Locked = 4,
    Disabled = 5,
    Deleted = 6
}

/// <summary>
/// Identity tags the first error of a failed Authenticate with why it failed and, when an account was found,
/// whose it was — so Protocol can audit the attempt. Never show this to the user: every failure must still be
/// answered the same way (no account enumeration). The password is never part of it.
/// </summary>
public static class AuthenticationFailure
{
    public const string ReasonMetadataKey = "AuthenticationFailureReason";
    public const string UserIdMetadataKey = "AuthenticationFailureUserId";

    public static Error Tag(Error error, FailureReason reason, Guid? userId)
    {
        error.WithMetadata(ReasonMetadataKey, reason);
        if (userId is { } id) error.WithMetadata(UserIdMetadataKey, id);
        return error;
    }

    /// <summary>The tagged reason and user id, or null when no error is tagged.</summary>
    public static (FailureReason Reason, Guid? UserId)? Read(IEnumerable<IError> errors)
    {
        foreach (var error in errors)
        {
            if (!error.Metadata.TryGetValue(ReasonMetadataKey, out var reason) || reason is not FailureReason failureReason)
                continue;

            var userId = error.Metadata.TryGetValue(UserIdMetadataKey, out var id) && id is Guid guid ? guid : (Guid?)null;
            return (failureReason, userId);
        }

        return null;
    }
}
