using System.Text.RegularExpressions;
using WardenId.SharedKernel.Domain;

namespace WardenId.Audit.Domain;

/// <summary>
/// Single documented catalog of known audit event types, and the datatype AuditEvent.EventType
/// carries. Because it's the type itself — not a runtime-validated string — a new audited
/// action cannot be emitted until its entry exists here: the compiler enforces the catalog,
/// there is no drift-and-silently-reject-or-accept failure mode. Key is the actual taxonomy
/// string persisted on the envelope; Name (inherited from Enumeration) is the C# identifier.
/// Grows alongside real capture call sites — do not pre-populate speculative event types.
/// </summary>
public sealed class AuditEventType : Enumeration<AuditEventType>
{
    private static readonly Regex KeyFormat = new(@"^[a-z0-9]+(\.[a-z0-9]+)+$", RegexOptions.Compiled);

    public string Key { get; }

    private AuditEventType(int value, string name, string key) : base(value, name)
    {
        if (!KeyFormat.IsMatch(key))
            throw new ArgumentException($"Audit event type key must be lowercase dot-notation, got '{key}'.", nameof(key));

        Key = key;
    }

    public static readonly AuditEventType UserSessionStart =
        new(1, nameof(UserSessionStart), "user.session.start");

    public static readonly AuditEventType UserLoginFailed =
        new(2, nameof(UserLoginFailed), "user.login.failed");

    public static readonly AuditEventType AdminTenantSuspend =
        new(3, nameof(AdminTenantSuspend), "admin.tenant.suspend");

    /// <summary>Generic admin.* CRUD capture (AuditCaptureInterceptor, I8) — reused across every IAuditableAggregate, not one entry per aggregate type. The specific entity is on AuditTarget instead.</summary>
    public static readonly AuditEventType AdminEntityCreate =
        new(4, nameof(AdminEntityCreate), "admin.entity.create");

    public static readonly AuditEventType AdminEntityUpdate =
        new(5, nameof(AdminEntityUpdate), "admin.entity.update");

    public static readonly AuditEventType AdminEntityDelete =
        new(6, nameof(AdminEntityDelete), "admin.entity.delete");

    /// <summary>RP-initiated logout (OIDC end session) by the signed-in user.</summary>
    public static readonly AuditEventType UserLogout =
        new(7, nameof(UserLogout), "user.logout");

    /// <summary>A login session ended, for any reason (logout, password change, disable, lockout, admin); the reason is the outcome reason.</summary>
    public static readonly AuditEventType UserSessionEnd =
        new(8, nameof(UserSessionEnd), "user.session.end");

    public static readonly AuditEventType TokenIssueAuthorizationCode =
        new(9, nameof(TokenIssueAuthorizationCode), "token.issue.authorizationcode");

    public static readonly AuditEventType TokenIssueRefreshToken =
        new(10, nameof(TokenIssueRefreshToken), "token.issue.refreshtoken");

    public static readonly AuditEventType TokenIssueClientCredentials =
        new(11, nameof(TokenIssueClientCredentials), "token.issue.clientcredentials");

    /// <summary>RFC 7009 revocation of a token (a refresh token's whole family with it).</summary>
    public static readonly AuditEventType TokenRevoke =
        new(12, nameof(TokenRevoke), "token.revoke");

    /// <summary>An already-redeemed refresh token was presented again; its family was revoked.</summary>
    public static readonly AuditEventType TokenRefreshReplay =
        new(13, nameof(TokenRefreshReplay), "token.refresh.replay");

    /// <summary>The checkpoint signing key was rotated; the target is the new key, the change is the active kid.</summary>
    public static readonly AuditEventType AuditCheckpointKeyRotate =
        new(14, nameof(AuditCheckpointKeyRotate), "admin.audit.checkpointkey.rotate");

    /// <summary>The user accepted the tenant's terms of service (sign-in terms step).</summary>
    public static readonly AuditEventType UserTermsAccept =
        new(15, nameof(UserTermsAccept), "user.terms.accept");

    /// <summary>The user set up a first authenticator app (sign-in enrollment step or account page).</summary>
    public static readonly AuditEventType UserMfaTotpEnroll =
        new(16, nameof(UserMfaTotpEnroll), "user.mfa.totp.enroll");

    /// <summary>The user replaced their authenticator app with a new one (account page).</summary>
    public static readonly AuditEventType UserMfaTotpReplace =
        new(17, nameof(UserMfaTotpReplace), "user.mfa.totp.replace");

    /// <summary>The user turned two-step verification off: every factor and recovery code removed (account page).</summary>
    public static readonly AuditEventType UserMfaRemove =
        new(18, nameof(UserMfaRemove), "user.mfa.remove");

    /// <summary>The user replaced their recovery codes with a new set (account page). The codes are never recorded.</summary>
    public static readonly AuditEventType UserMfaRecoveryCodesRegenerate =
        new(19, nameof(UserMfaRecoveryCodesRegenerate), "user.mfa.recoverycodes.regenerate");

    /// <summary>The user changed their own password (account page). Admin changes are admin.entity.update.</summary>
    public static readonly AuditEventType UserPasswordChange =
        new(20, nameof(UserPasswordChange), "user.password.change");

    /// <summary>
    /// The password or code check in front of a self-service account change failed; the outcome reason says why
    /// (invalid_password, invalid_second_factor, account_locked_out, ...).
    /// </summary>
    public static readonly AuditEventType UserStepUpFailed =
        new(21, nameof(UserStepUpFailed), "user.stepup.failed");

    public static readonly IReadOnlyList<AuditEventType> All =
    [
        UserSessionStart,
        UserLoginFailed,
        AdminTenantSuspend,
        AdminEntityCreate,
        AdminEntityUpdate,
        AdminEntityDelete,
        UserLogout,
        UserSessionEnd,
        TokenIssueAuthorizationCode,
        TokenIssueRefreshToken,
        TokenIssueClientCredentials,
        TokenRevoke,
        TokenRefreshReplay,
        AuditCheckpointKeyRotate,
        UserTermsAccept,
        UserMfaTotpEnroll,
        UserMfaTotpReplace,
        UserMfaRemove,
        UserMfaRecoveryCodesRegenerate,
        UserPasswordChange,
        UserStepUpFailed
    ];

    /// <summary>Resolves a catalog entry from its persisted taxonomy string — canonical deserialization only.</summary>
    public static AuditEventType? FromKey(string key) => All.FirstOrDefault(t => t.Key == key);
}
