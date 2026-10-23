using System.Text.RegularExpressions;
using WardenId.SharedKernel.Domain;

namespace WardenId.Audit.Domain;

/// <summary>
/// Single documented catalog of machine-readable outcome reason codes (OAuth-style, e.g.
/// "invalid_grant", "unknown_username"). Fastest-growing of the audit catalogs — unlike
/// AuditTargetType/AuditCreationSource this isn't tied to a small closed concept, it's
/// roughly one code per failure mode across every channel, so expect the most additions
/// here over time. Same enforcement rationale as AuditEventType: it's the type
/// AuditOutcome.Reason carries, so a new failure mode cannot be recorded until its entry
/// exists here. Key is the persisted taxonomy string; Name (inherited from Enumeration) is
/// the C# identifier. Grows alongside real capture call sites — do not pre-populate
/// speculative reasons.
/// </summary>
public sealed class AuditOutcomeReason : Enumeration<AuditOutcomeReason>
{
    private static readonly Regex KeyFormat = new(AuditFormats.KeyPatterns.SnakeCase, RegexOptions.Compiled);

    public string Key { get; }

    private AuditOutcomeReason(int value, string name, string key) : base(value, name)
    {
        if (!KeyFormat.IsMatch(key))
            throw new ArgumentException($"Audit outcome reason key must be lowercase snake_case, got '{key}'.", nameof(key));

        Key = key;
    }

    public static readonly AuditOutcomeReason InvalidGrant = new(1, nameof(InvalidGrant), "invalid_grant");

    public static readonly AuditOutcomeReason UnknownUsername = new(2, nameof(UnknownUsername), "unknown_username");

    public static readonly AuditOutcomeReason PolicyDenied = new(3, nameof(PolicyDenied), "policy_denied");

    public static readonly AuditOutcomeReason InvalidPassword = new(4, nameof(InvalidPassword), "invalid_password");

    /// <summary>Wrong password, and this attempt locked the account.</summary>
    public static readonly AuditOutcomeReason AccountLockedOut = new(5, nameof(AccountLockedOut), "account_locked_out");

    /// <summary>Rejected because the account is locked; also the end reason of sessions ended by a lockout.</summary>
    public static readonly AuditOutcomeReason AccountLocked = new(6, nameof(AccountLocked), "account_locked");

    public static readonly AuditOutcomeReason AccountDisabled = new(7, nameof(AccountDisabled), "account_disabled");

    public static readonly AuditOutcomeReason AccountDeleted = new(8, nameof(AccountDeleted), "account_deleted");

    /// <summary>The credential check failed for a reason the identity store did not classify.</summary>
    public static readonly AuditOutcomeReason InvalidCredentials = new(9, nameof(InvalidCredentials), "invalid_credentials");

    public static readonly AuditOutcomeReason EmailNotVerified = new(10, nameof(EmailNotVerified), "email_not_verified");

    public static readonly AuditOutcomeReason PasswordResetRequired = new(11, nameof(PasswordResetRequired), "password_reset_required");

    public static readonly AuditOutcomeReason TermsNotAccepted = new(12, nameof(TermsNotAccepted), "terms_not_accepted");

    public static readonly AuditOutcomeReason MfaEnrollmentRequired = new(13, nameof(MfaEnrollmentRequired), "mfa_enrollment_required");

    public static readonly AuditOutcomeReason PendingVerification = new(14, nameof(PendingVerification), "pending_verification");

    public static readonly AuditOutcomeReason UserLogout = new(15, nameof(UserLogout), "user_logout");

    public static readonly AuditOutcomeReason PasswordChanged = new(16, nameof(PasswordChanged), "password_changed");

    public static readonly AuditOutcomeReason UserDisabled = new(17, nameof(UserDisabled), "user_disabled");

    public static readonly AuditOutcomeReason UserDeleted = new(18, nameof(UserDeleted), "user_deleted");

    public static readonly AuditOutcomeReason AdminEnded = new(19, nameof(AdminEnded), "admin_ended");

    /// <summary>A wrong TOTP or recovery code at sign-in, or at an account page's step-up.</summary>
    public static readonly AuditOutcomeReason InvalidSecondFactor = new(20, nameof(InvalidSecondFactor), "invalid_second_factor");

    /// <summary>Session end reason: the user replaced or removed their authenticator, which ends their other sessions.</summary>
    public static readonly AuditOutcomeReason MfaChanged = new(21, nameof(MfaChanged), "mfa_changed");

    public static readonly IReadOnlyList<AuditOutcomeReason> All =
    [
        InvalidGrant,
        UnknownUsername,
        PolicyDenied,
        InvalidPassword,
        AccountLockedOut,
        AccountLocked,
        AccountDisabled,
        AccountDeleted,
        InvalidCredentials,
        EmailNotVerified,
        PasswordResetRequired,
        TermsNotAccepted,
        MfaEnrollmentRequired,
        PendingVerification,
        UserLogout,
        PasswordChanged,
        UserDisabled,
        UserDeleted,
        AdminEnded,
        InvalidSecondFactor,
        MfaChanged
    ];

    /// <summary>Resolves a catalog entry from its persisted taxonomy string — canonical deserialization only.</summary>
    public static AuditOutcomeReason? FromKey(string key) => All.FirstOrDefault(r => r.Key == key);
}
