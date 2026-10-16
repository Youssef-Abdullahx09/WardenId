using WardenId.Audit.Domain;

namespace WardenId.Audit.Application.Features.Events.List;

/// <summary>
/// One event, summarized from its payload. When the payload is gone (retention) or can't be read,
/// only the table columns are filled and <see cref="PayloadAvailable"/> is false.
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
