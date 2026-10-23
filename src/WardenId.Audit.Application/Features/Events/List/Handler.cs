using FluentResults;
using MediatR;
using WardenId.Audit.Domain;
using WardenId.Audit.Query.Contracts;
using WardenId.SharedKernel.Query.Contracts;
using AuditEventRow = WardenId.Audit.Query.Contracts.Features.AuditEvents.List.Item;
using AuditEventsListQuery = WardenId.Audit.Query.Contracts.Features.AuditEvents.List.Query;

namespace WardenId.Audit.Application.Features.Events.List;

/// <summary>Read side: rows come from the Dapper query layer (Audit.Query); payloads are decoded and summarized here.</summary>
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

        var result = await auditEventsQuery.List(
            new AuditEventsListQuery(
                request.Channel?.ToString(),
                request.TenantId,
                request.From,
                request.To,
                Normalize(request.EventType),
                Normalize(request.Actor),
                Normalize(request.Target),
                request.Outcome?.ToString(),
                request.PageNumber,
                request.PageSize),
            cancellationToken);
        if (result.IsFailed) return Result.Fail(result.Errors);

        return Result.Ok(new PaginationListResponse<AuditEventListItem>(
            result.Value.RecordsTotal,
            result.Value.Data.Select(Summarize).ToArray()));
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>From the payload when it can be read; otherwise from the row's search columns.</summary>
    private static AuditEventListItem Summarize(AuditEventRow row)
    {
        var channel = AuditEventColumns.ParseChannel(row.Channel);
        var occurredAt = AuditEventColumns.AsUtc(row.OccurredAt);

        var auditEvent = AuditPayloadDecoder.TryDecode(row.Payload);
        if (auditEvent is null)
        {
            return new AuditEventListItem(
                row.Id, channel, row.TenantId, row.Sequence, occurredAt,
                PayloadAvailable: false,
                row.EventType,
                Severity: null,
                AuditEventColumns.ParseOutcome(row.Outcome),
                OutcomeReason: null,
                row.Actor,
                row.Targets);
        }

        return new AuditEventListItem(
            row.Id,
            channel,
            row.TenantId,
            row.Sequence,
            occurredAt,
            PayloadAvailable: true,
            auditEvent.EventType.Key,
            auditEvent.Severity,
            auditEvent.Outcome.Status,
            auditEvent.Outcome.Reason?.Key,
            auditEvent.Actor.ToSerializedForm(),
            auditEvent.Targets.Select(t => t.ToSerializedForm()).ToArray());
    }
}
