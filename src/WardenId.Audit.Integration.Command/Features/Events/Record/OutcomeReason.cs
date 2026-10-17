namespace WardenId.Audit.Integration.Command.Features.Events.Record;

public enum OutcomeReason
{
    // Sign-in failures.
    UnknownUser = 1,
    InvalidPassword = 2,

    /// <summary>Wrong password, and this attempt locked the account.</summary>
    AccountLockedOut = 3,

    /// <summary>Rejected because the account is locked (also a session end reason).</summary>
    AccountLocked = 4,
    AccountDisabled = 5,
    AccountDeleted = 6,

    /// <summary>The credential check failed for a reason Identity did not classify.</summary>
    InvalidCredentials = 7,
    EmailNotVerified = 8,
    PasswordResetRequired = 9,
    TermsNotAccepted = 10,
    MfaEnrollmentRequired = 11,
    PendingVerification = 12,

    // Session end reasons.
    UserLogout = 13,
    PasswordChanged = 14,
    UserDisabled = 15,
    UserDeleted = 16,
    AdminEnded = 17,

    // Token failures.
    InvalidGrant = 18,

    /// <summary>A wrong TOTP or recovery code at sign-in (also a wrong first code while enrolling).</summary>
    InvalidSecondFactor = 19
}
