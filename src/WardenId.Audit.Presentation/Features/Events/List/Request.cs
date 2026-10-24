using WardenId.Audit.Query.Contracts.Features.AuditEvents;
using WardenId.Audit.Query.Contracts.Features.AuditEvents.List;
using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.Audit.Presentation.Features.Events.List;

// Aliased inside the namespace: WardenId.Audit.Query (the Dapper read side) would otherwise shadow the request type.
using Query = WardenId.Audit.Application.Features.Events.List.Query;

public sealed record Request(
    int PageNumber = 1,
    int PageSize = 10,
    SortDirection SortDirection = SortDirection.Desc,
    AuditChannel? Channel = null,
    Guid? TenantId = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? EventType = null,
    string? Actor = null,
    string? Target = null,
    AuditResult? Outcome = null,
    SortOption SortOption = SortOption.OccurredAt) : PaginationListRequest(PageNumber, PageSize, SortDirection)
{
    public Query ToQuery() =>
        new(PageNumber, PageSize, SortDirection)
        {
            Channel = Channel,
            TenantId = TenantId,
            From = From,
            To = To,
            EventType = EventType,
            Actor = Actor,
            Target = Target,
            Outcome = Outcome,
            SortOption = SortOption
        };
}
