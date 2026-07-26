using WardenId.ClientManagement.Domain;

namespace WardenId.ClientManagement.Application.Features.Clients.List;

public sealed record ClientListItem(
    Guid Id,
    string ClientId,
    string DisplayName,
    ClientType ClientType,
    bool Enabled,
    bool IsFirstParty,
    DateTimeOffset CreatedAt);
