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

        var (total, rows) = await search.SearchAsync(
            new AuditEventFilter(request.Channel, request.TenantId, request.From, request.To),
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        return Result.Ok(new PaginationListResponse<AuditEventListItem>(total, rows.Select(Summarize).ToArray()));
    }

    private static AuditEventListItem Summarize(AuditEventSearchRow row)
    {
        var auditEvent = Decode(row.Payload);
        return new AuditEventListItem(
            row.Id,
            row.Channel,
            row.TenantId,
            row.Sequence,
            row.OccurredAt,
            PayloadAvailable: auditEvent is not null,
            auditEvent?.EventType.Key,
            auditEvent?.Severity,
            auditEvent?.Outcome.Status,
            auditEvent?.Outcome.Reason?.Key,
            auditEvent?.Actor.ToSerializedForm(),
            auditEvent?.Targets.Select(t => $"{t.Type.Key}:{t.Id}").ToArray() ?? []);
    }

    private static AuditEvent? Decode(byte[]? payload)
    {
        if (payload is null) return null;
        try
        {
            return CanonicalAuditEventSerializer.Deserialize(payload);
        }
        catch (Exception)
        {
            // Unreadable: an event type this build doesn't know, or bytes that aren't a canonical event
            // (the serializer throws more than one exception type for those). A summary is best effort,
            // so one bad row must not fail the page; it's still listed and has its inclusion proof.
            return null;
        }
    }
}
