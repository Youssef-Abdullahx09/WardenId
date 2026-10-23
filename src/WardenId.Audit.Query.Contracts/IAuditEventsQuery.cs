using FluentResults;
using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.Audit.Query.Contracts;

/// <summary>Read-side (Dapper) queries over audit.events. Payloads come back raw; decoding them is the caller's job.</summary>
public interface IAuditEventsQuery
{
    Task<Result<PaginationListResponse<Features.AuditEvents.List.Item>>> List(
        Features.AuditEvents.List.Query query,
        CancellationToken cancellationToken);

    /// <summary>The event, or a null value when no row has that id.</summary>
    Task<Result<Features.AuditEvents.Get.Item?>> Get(
        Features.AuditEvents.Get.Query query,
        CancellationToken cancellationToken);
}
