using ListQuery = WardenId.Protocol.Application.Features.Sessions.List.Query;

namespace WardenId.Protocol.Presentation.Features.Sessions.List;

public sealed record Request(
    int PageNumber,
    int PageSize,
    Guid? UserId,
    string? ClientId,
    bool? IncludeEnded)
{
    public ListQuery ToQuery(Guid tenantId) =>
        new(tenantId, PageNumber, PageSize, UserId, string.IsNullOrWhiteSpace(ClientId) ? null : ClientId, IncludeEnded ?? false);
}
