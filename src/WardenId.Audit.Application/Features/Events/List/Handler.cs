using FluentResults;
using MediatR;
using WardenId.Audit.Domain;
using WardenId.Audit.Query.Contracts;
using WardenId.Audit.Query.Contracts.Features.AuditEvents.List;
using WardenId.SharedKernel.Query.Contracts;
using AuditEventRow = WardenId.Audit.Query.Contracts.Features.AuditEvents.List.Item;
using AuditEventsListQuery = WardenId.Audit.Query.Contracts.Features.AuditEvents.List.Query;
using AuditResult = WardenId.Audit.Query.Contracts.Features.AuditEvents.AuditResult;

namespace WardenId.Audit.Application.Features.Events.List;

/// <summary>
/// Read side: rows come from the Dapper query layer (Audit.Query). Each payload is decoded and summarized here, which
/// needs the domain's canonical serializer and so can't move into SQL.
/// </summary>
public sealed class Handler(IAuditEventsQuery auditEventsQuery)
    : IRequestHandler<Query, Result<PaginationListResponse<AuditEventListItem>>>
{
    public const int MaxPageSize = 100;

    public async Task<Result<PaginationListResponse<AuditEventListItem>>> Handle(
        Query request,
        CancellationToken cancellationToken)
    {
        if (request.PageNumber < 1)
            return Result.Fail(AuditErrors.Validation("Page number must be at least 1."));
        if (request.PageSize is < 1 or > MaxPageSize)
            return Result.Fail(AuditErrors.Validation($"Page size must be between 1 and {MaxPageSize}."));
        if (request.From is { } from && request.To is { } to && from > to)
            return Result.Fail(AuditErrors.Validation("'from' must not be after 'to'."));
        // Minimal-API enum binding also accepts numbers, so an undefined member can get this far.
        if (!Enum.IsDefined(request.SortOption) || !Enum.IsDefined(request.SortDirection))
            return Result.Fail(AuditErrors.Validation(
                $"SortOption must be one of {string.Join(", ", Enum.GetNames<SortOption>())} " +
                $"and SortDirection one of {string.Join(", ", Enum.GetNames<SortDirection>())}."));

        var result = await auditEventsQuery.List(
            new AuditEventsListQuery(request.PageNumber, request.PageSize, request.SortDirection)
            {
                Channel = request.Channel,
                TenantId = request.TenantId,
                From = request.From,
                To = request.To,
                EventType = request.EventType,
                Actor = request.Actor,
                Target = request.Target,
                Outcome = request.Outcome,
                SortOption = request.SortOption
            },
            cancellationToken);
        if (result.IsFailed) return Result.Fail(result.Errors);

        return Result.Ok(new PaginationListResponse<AuditEventListItem>(
            result.Value.RecordsTotal,
            result.Value.Data.Select(Summarize).ToArray()));
    }

    /// <summary>From the payload when it can be read; otherwise from the row's search columns.</summary>
    private static AuditEventListItem Summarize(AuditEventRow row)
    {
        var occurredAt = AuditEventColumns.AsUtc(row.OccurredAt);

        var auditEvent = AuditPayloadDecoder.TryDecode(row.Payload);
        if (auditEvent is null)
        {
            return new AuditEventListItem(
                row.Id, row.Channel, row.TenantId, row.Sequence, occurredAt,
                PayloadAvailable: false,
                row.EventType,
                Severity: null,
                row.Outcome,
                OutcomeReason: null,
                row.Actor,
                row.Targets);
        }

        return new AuditEventListItem(
            row.Id,
            row.Channel,
            row.TenantId,
            row.Sequence,
            occurredAt,
            PayloadAvailable: true,
            auditEvent.EventType.Key,
            auditEvent.Severity,
            // The read model mirrors the domain enum member for member (AuditEventEnums.cs), so they convert by cast.
            (AuditResult)auditEvent.Outcome.Status,
            auditEvent.Outcome.Reason?.Key,
            auditEvent.Actor.ToSerializedForm(),
            auditEvent.Targets.Select(t => t.ToSerializedForm()).ToArray());
    }
}
