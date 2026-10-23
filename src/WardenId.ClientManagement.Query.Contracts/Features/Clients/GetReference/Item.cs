namespace WardenId.ClientManagement.Query.Contracts.Features.Clients.GetReference;

/// <summary>One row of Features/Clients/GetReference/Query.sql.</summary>
public sealed class Item
{
    public Guid Id { get; init; }
    public string ClientId { get; init; } = string.Empty;
}
