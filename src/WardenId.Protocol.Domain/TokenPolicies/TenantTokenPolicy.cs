using WardenId.SharedKernel.Domain;

namespace WardenId.Protocol.Domain.TokenPolicies;

/// <summary>
/// A tenant's token and login-session lifetime overrides (FR-PC-08), one row per tenant. A null field falls back
/// to the global default; a client's own lifetime overrides (ClientManagement) still win over these. Changes
/// apply to tokens and sessions issued afterwards; nothing already issued is touched.
/// </summary>
public sealed class TenantTokenPolicy : AggregateRoot, IAuditableAggregate
{
    private static readonly IReadOnlySet<string> NothingSensitive = new HashSet<string>();

    // Lifetimes are tenant configuration: admin.config, filed under the tenant they belong to.
    public string AuditTargetTypeKey => "tenant";
    public AuditableChannel AuditChannel => AuditableChannel.AdminConfig;
    public IReadOnlySet<string> AuditSensitivePropertyNames => NothingSensitive;
    public string? AuditTargetId => TenantId.ToString();

    private TenantTokenPolicy(Guid tenantId) => TenantId = tenantId;

    public Guid TenantId { get; private set; }
    public TimeSpan? AccessTokenLifetime { get; private set; }
    public TimeSpan? IdTokenLifetime { get; private set; }
    public TimeSpan? RefreshTokenLifetime { get; private set; }
    public TimeSpan? RefreshTokenAbsoluteLifetime { get; private set; }
    public TimeSpan? AuthorizationCodeLifetime { get; private set; }
    public TimeSpan? SessionCookieLifetime { get; private set; }
    public TimeSpan? SessionAbsoluteLifetime { get; private set; }

    public TenantLifetimeOverrides Overrides => TenantLifetimeOverrides.FromTrustedSource(
        AccessTokenLifetime, IdTokenLifetime, RefreshTokenLifetime, RefreshTokenAbsoluteLifetime,
        AuthorizationCodeLifetime, SessionCookieLifetime, SessionAbsoluteLifetime);

    public static TenantTokenPolicy Create(Guid tenantId, TenantLifetimeOverrides overrides)
    {
        var policy = new TenantTokenPolicy(tenantId);
        policy.Replace(overrides);
        return policy;
    }

    /// <summary>Replaces every override (PUT semantics): a null field clears it back to the global default.</summary>
    public void Replace(TenantLifetimeOverrides overrides)
    {
        AccessTokenLifetime = overrides.AccessToken;
        IdTokenLifetime = overrides.IdToken;
        RefreshTokenLifetime = overrides.RefreshToken;
        RefreshTokenAbsoluteLifetime = overrides.RefreshTokenAbsolute;
        AuthorizationCodeLifetime = overrides.AuthorizationCode;
        SessionCookieLifetime = overrides.SessionCookie;
        SessionAbsoluteLifetime = overrides.SessionAbsolute;
    }
}
