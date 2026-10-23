using System.Text.RegularExpressions;
using WardenId.SharedKernel.Domain;

namespace WardenId.Audit.Domain;

/// <summary>
/// Single documented catalog of target kinds — roughly one per aggregate root across the
/// whole system (Tenant, User, OAuthClient, SigningKey, ...), so it grows much slower than
/// AuditEventType. Same reasoning applies: it's the type AuditTarget.Type carries, not a
/// runtime-validated string, so a new target kind cannot be emitted until its entry exists
/// here — the compiler enforces the catalog. Key is the persisted taxonomy string; Name
/// (inherited from Enumeration) is the C# identifier. Grows alongside real capture call
/// sites — do not pre-populate speculative target kinds.
/// </summary>
public sealed class AuditTargetType : Enumeration<AuditTargetType>
{
    private static readonly Regex KeyFormat = new(AuditFormats.KeyPatterns.SnakeCase, RegexOptions.Compiled);

    public string Key { get; }

    private AuditTargetType(int value, string name, string key) : base(value, name)
    {
        if (!KeyFormat.IsMatch(key))
            throw new ArgumentException($"Audit target type key must be lowercase snake_case, got '{key}'.", nameof(key));

        Key = key;
    }

    public static readonly AuditTargetType Tenant = new(1, nameof(Tenant), AuditTargetTypeKeys.Tenant);
    public static readonly AuditTargetType User = new(2, nameof(User), AuditTargetTypeKeys.User);

    /// <summary>A login session; the id is the OIDC sid.</summary>
    public static readonly AuditTargetType Session = new(3, nameof(Session), AuditTargetTypeKeys.Session);

    /// <summary>An OAuth client; the id is its public client_id.</summary>
    public static readonly AuditTargetType OAuthClient = new(4, nameof(OAuthClient), AuditTargetTypeKeys.OAuthClient);

    /// <summary>A grant's token family; the id is the OpenIddict authorization id.</summary>
    public static readonly AuditTargetType TokenFamily = new(5, nameof(TokenFamily), AuditTargetTypeKeys.TokenFamily);

    /// <summary>A key that signs audit checkpoints; the id is its kid.</summary>
    public static readonly AuditTargetType CheckpointSigningKey = new(6, nameof(CheckpointSigningKey), AuditTargetTypeKeys.CheckpointSigningKey);

    /// <summary>A tenant's identity policy overrides (Identity module); the id is the tenant id, one policy per tenant.</summary>
    public static readonly AuditTargetType IdentityPolicy = new(7, nameof(IdentityPolicy), AuditTargetTypeKeys.IdentityPolicy);

    public static readonly IReadOnlyList<AuditTargetType> All =
    [
        Tenant,
        User,
        Session,
        OAuthClient,
        TokenFamily,
        CheckpointSigningKey,
        IdentityPolicy
    ];

    /// <summary>Resolves a catalog entry from its persisted taxonomy string — canonical deserialization only.</summary>
    public static AuditTargetType? FromKey(string key) => All.FirstOrDefault(t => t.Key == key);
}
