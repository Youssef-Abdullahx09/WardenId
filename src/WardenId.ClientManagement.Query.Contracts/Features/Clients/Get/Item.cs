namespace WardenId.ClientManagement.Query.Contracts.Features.Clients.Get;

/// <summary>
/// The client row of Features/Clients/Get/Query.sql (first result set), with the secrets and signing keys of the
/// second and third result sets attached. Enums come back as their persisted member names.
/// </summary>
public sealed class Item
{
    public Guid Id { get; init; }
    public string ClientId { get; init; } = string.Empty;
    public Guid TenantId { get; init; }

    /// <summary>The <c>ClientType</c> member name, as persisted.</summary>
    public string ClientType { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool Enabled { get; init; }
    public bool IsFirstParty { get; init; }

    /// <summary>The <c>GrantType</c> flags, as persisted (comma-separated member names).</summary>
    public string GrantType { get; init; } = string.Empty;

    public bool RequirePkce { get; init; }

    /// <summary>The <c>ClientAuthenticationMethod</c> member name, as persisted.</summary>
    public string ClientAuthenticationMethod { get; init; } = string.Empty;

    public bool ConsentRequired { get; init; }
    public int? AccessTokenSeconds { get; init; }
    public int? IdTokenSeconds { get; init; }
    public int? RefreshTokenSeconds { get; init; }
    public string[] RedirectUris { get; init; } = [];
    public string[] PostLogoutRedirectUris { get; init; } = [];
    public string[] CorsOrigins { get; init; } = [];
    public Guid[] AssignedScopeIds { get; init; } = [];
    public string? BackchannelLogoutUri { get; init; }
    public Secret[] Secrets { get; set; } = [];
    public SigningKey[] SigningKeys { get; set; } = [];
}
