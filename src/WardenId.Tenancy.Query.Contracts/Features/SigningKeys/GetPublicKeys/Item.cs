namespace WardenId.Tenancy.Query.Contracts.Features.SigningKeys.GetPublicKeys;

/// <summary>The tenant row of Features/SigningKeys/GetPublicKeys/Query.sql, plus its keys from the second result set.</summary>
public sealed class Item
{
    public Guid Id { get; init; }
    public string Slug { get; init; } = string.Empty;

    /// <summary>The tenant <c>Status</c> member name, as persisted.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Filled from the second result set, not a column.</summary>
    public List<PublicKeyItem> PublicKeys { get; set; } = [];
}

public sealed class PublicKeyItem
{
    /// <summary>The <c>SigningKeyStatus</c> member name, as persisted.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>The stored JWK JSON (jsonb read as text), verbatim.</summary>
    public string PublicKeyJwk { get; init; } = string.Empty;
}
