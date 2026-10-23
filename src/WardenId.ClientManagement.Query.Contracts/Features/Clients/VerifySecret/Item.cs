namespace WardenId.ClientManagement.Query.Contracts.Features.Clients.VerifySecret;

/// <summary>
/// The client row of Features/Clients/VerifySecret/Query.sql (first result set), with the secrets of the second
/// attached. Enums come back as their persisted member names.
/// </summary>
public sealed class Item
{
    public bool Enabled { get; init; }

    /// <summary>The <c>ClientType</c> member name, as persisted.</summary>
    public string ClientType { get; init; } = string.Empty;

    /// <summary>The <c>ClientAuthenticationMethod</c> member name, as persisted.</summary>
    public string ClientAuthenticationMethod { get; init; } = string.Empty;

    public Secret[] Secrets { get; set; } = [];
}
