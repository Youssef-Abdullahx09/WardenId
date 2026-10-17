namespace WardenId.Audit.Integration.Command.Features.Events.Record;

public enum SecurityEvent
{
    SignInFailed = 1,
    SessionStarted = 2,
    SignedOut = 3,
    SessionEnded = 4,
    TokenIssuedAuthorizationCode = 5,
    TokenIssuedRefreshToken = 6,
    TokenIssuedClientCredentials = 7,
    TokenRevoked = 8,
    RefreshTokenReplayed = 9,

    // The user's own account changes (the sign-in steps after the password, and the account pages). Actor and
    // target are the user; recorded once Identity has made the change.

    /// <summary>The user accepted the tenant's terms (the sign-in terms step).</summary>
    TermsAccepted = 10,

    /// <summary>A first authenticator app was set up, at sign-in or from the account page.</summary>
    TotpEnrolled = 11,

    /// <summary>A new authenticator app replaced the user's current one (account page).</summary>
    AuthenticatorReplaced = 12,

    /// <summary>Every factor and recovery code was removed (account page).</summary>
    MfaRemoved = 13,

    /// <summary>A new set of recovery codes replaced the old one (account page).</summary>
    RecoveryCodesRegenerated = 14,

    /// <summary>The user changed their own password after giving the current one (account page).</summary>
    PasswordChanged = 15,

    /// <summary>
    /// A check in front of an account change failed: a wrong current password, a wrong code of the current
    /// authenticator or a recovery code, or a wrong first code of a new authenticator. Requires a reason.
    /// </summary>
    StepUpFailed = 16
}
