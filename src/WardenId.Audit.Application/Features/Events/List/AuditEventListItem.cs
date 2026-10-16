using WardenId.Audit.Domain;

namespace WardenId.Audit.Application.Features.Events.List;

/// <summary>
/// One event, summarized from its payload. When the payload is gone (retention) or can't be read,
/// <see cref="PayloadAvailable"/> is false and the summary comes from the row's search columns
/// (event type, actor, targets, result); severity and outcome reason are then null, as are the
/// search columns on rows purged before those columns existed.
/// </summary>
public sealed record AuditEventListItem(
    Guid Id,
    AuditChannel Channel,
    Guid? TenantId,
    long? Sequence,
    DateTimeOffset OccurredAt,
    bool PayloadAvailable,
    string? EventType,
    AuditSeverity? Severity,
    AuditResult? Result,
    string? OutcomeReason,
    string? Actor,
    IReadOnlyList<string> Targets);
