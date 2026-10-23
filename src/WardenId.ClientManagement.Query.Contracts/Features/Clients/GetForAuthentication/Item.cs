namespace WardenId.ClientManagement.Query.Contracts.Features.Clients.GetForAuthentication;

/// <summary>One row of Features/Clients/GetForAuthentication/Query.sql. Enums come back as their persisted member names.</summary>
public sealed class Item
{
    public Guid Id { get; init; }
    public string ClientId { get; init; } = string.Empty;

    /// <summary>The <c>ClientType</c> member name, as persisted.</summary>
    public string ClientType { get; init; } = string.Empty;

    public bool Enabled { get; init; }

    /// <summary>The <c>GrantType</c> flags, as persisted (comma-separated member names).</summary>
    public string GrantType { get; init; } = string.Empty;

    /// <summary>The <c>ClientAuthenticationMethod</c> member name, as persisted.</summary>
    public string ClientAuthenticationMethod { get; init; } = string.Empty;

    public int? AccessTokenSeconds { get; init; }
    public int? IdTokenSeconds { get; init; }
    public int? RefreshTokenSeconds { get; init; }
    public string[] RedirectUris { get; init; } = [];
    public bool RequirePkce { get; init; }
    public string[] PostLogoutRedirectUris { get; init; } = [];
    public string? BackchannelLogoutUri { get; init; }
}
