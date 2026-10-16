using FluentResults;
using MediatR;
using WardenId.Audit.Application.Abstractions;
using WardenId.Audit.Domain;
using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.Audit.Application.Features.Events.List;

public sealed class Handler(IAuditEventSearch search)
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

        var filter = new AuditEventFilter(
            request.Channel,
            request.TenantId,
            request.From,
            request.To,
            Normalize(request.EventType),
            Normalize(request.Actor),
            Normalize(request.Target),
            request.Outcome);

        var (total, rows) = await search.SearchAsync(filter, request.PageNumber, request.PageSize, cancellationToken);

        return Result.Ok(new PaginationListResponse<AuditEventListItem>(total, rows.Select(Summarize).ToArray()));
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>From the payload when it can be read; otherwise from the row's search columns.</summary>
    private static AuditEventListItem Summarize(AuditEventSearchRow row)
    {
        var auditEvent = AuditPayloadDecoder.TryDecode(row.Payload);
        if (auditEvent is null)
        {
            return new AuditEventListItem(
                row.Id, row.Channel, row.TenantId, row.Sequence, row.OccurredAt,
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
            row.OccurredAt,
            PayloadAvailable: true,
            auditEvent.EventType.Key,
            auditEvent.Severity,
            auditEvent.Outcome.Status,
            auditEvent.Outcome.Reason?.Key,
            auditEvent.Actor.ToSerializedForm(),
            auditEvent.Targets.Select(t => t.ToSerializedForm()).ToArray());
    }
}
