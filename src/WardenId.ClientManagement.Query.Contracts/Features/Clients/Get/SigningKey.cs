namespace WardenId.ClientManagement.Query.Contracts.Features.Clients.Get;

/// <summary>One row of the signing-keys result set. Times are UTC <see cref="DateTime"/> (Npgsql reads timestamptz as Kind=Utc).</summary>
public sealed class SigningKey
{
    public Guid Id { get; init; }
    public string Kid { get; init; } = string.Empty;
    public string Algorithm { get; init; } = string.Empty;
    public DateTime? ExpiresAt { get; init; }
}
