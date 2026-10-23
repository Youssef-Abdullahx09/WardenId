namespace WardenId.ClientManagement.Query.Contracts.Features.Clients.Get;

/// <summary>One row of the secrets result set. Times are UTC <see cref="DateTime"/> (Npgsql reads timestamptz as Kind=Utc).</summary>
public sealed class Secret
{
    public Guid Id { get; init; }
    public string Label { get; init; } = string.Empty;
    public DateTime? ExpiresAt { get; init; }
}
