namespace WardenId.ClientManagement.Query.Contracts.Features.Clients.List;

/// <summary>One page of a tenant's non-deleted clients, newest <c>CreatedAt</c> first.</summary>
/// <param name="Enabled">Only clients in this state; null for any.</param>
/// <param name="ClientType">Only clients of this <c>ClientType</c> member name; null for any.</param>
public sealed record Query(
    Guid TenantId,
    bool? Enabled,
    string? ClientType,
    int PageNumber,
    int PageSize);
