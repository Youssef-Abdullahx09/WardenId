namespace WardenId.Tenancy.Query.Contracts.Features.SigningKeys.GetActiveForSigning;

/// <summary>
/// One row of Features/SigningKeys/GetActiveForSigning/Query.sql. The key columns are null when the tenant exists but
/// has no active key. <see cref="EncryptedPrivateKey"/> is the stored bytea, still protected — Tenancy never decrypts it.
/// </summary>
public sealed class Item
{
    public string Slug { get; init; } = string.Empty;
    public Guid? SigningKeyId { get; init; }
    public string? Algorithm { get; init; }
    public byte[]? EncryptedPrivateKey { get; init; }
}
