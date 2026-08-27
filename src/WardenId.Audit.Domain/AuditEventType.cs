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

    public static readonly IReadOnlyList<AuditEventType> All =
    [
        UserSessionStart,
        UserLoginFailed,
        AdminTenantSuspend,
        AdminEntityCreate,
        AdminEntityUpdate,
        AdminEntityDelete
    ];

    /// <summary>Resolves a catalog entry from its persisted taxonomy string — canonical deserialization only.</summary>
    public static AuditEventType? FromKey(string key) => All.FirstOrDefault(t => t.Key == key);
}
