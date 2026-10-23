namespace WardenId.ClientManagement.Query.Contracts.Features.Clients.VerifySecret;

/// <summary>One row of the secrets result set. <c>ExpiresAt</c> is a UTC <see cref="DateTime"/> (Npgsql reads timestamptz as Kind=Utc).</summary>
public sealed class Secret
{
    public string HashedValue { get; init; } = string.Empty;
    public DateTime? ExpiresAt { get; init; }
}
