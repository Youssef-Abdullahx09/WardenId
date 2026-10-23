namespace WardenId.Tenancy.Query.Contracts.Features.Tenants.Get;

/// <summary>
/// The tenant row of Features/Tenants/Get/Query.sql (and GetBySlug/Query.sql), plus its signing keys from the second
/// result set. Times are UTC <see cref="DateTime"/>: Npgsql reads timestamptz as DateTime (Kind=Utc).
/// </summary>
public sealed class Item
{
    public Guid Id { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>The tenant <c>Status</c> member name, as persisted.</summary>
    public string Status { get; init; } = string.Empty;

    public DateTime? SuspendedAtUtc { get; init; }
    public string? SuspendedReason { get; init; }
    public DateTime CreatedAt { get; init; }

    /// <summary>Newest first. Filled from the second result set, not a column.</summary>
    public List<SigningKeyItem> SigningKeys { get; set; } = [];
}

/// <summary>Public metadata of one signing key; no key material.</summary>
public sealed class SigningKeyItem
{
    public Guid SigningKeyId { get; init; }
    public string Algorithm { get; init; } = string.Empty;

    /// <summary>The <c>SigningKeyStatus</c> member name, as persisted.</summary>
    public string Status { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }
    public DateTime ActivatedAt { get; init; }
    public DateTime? RetiringSince { get; init; }
    public DateTime? RetiredAt { get; init; }
}
