using FluentResults;
using MediatR;

namespace WardenId.Audit.Integration.Command.Features.Events.Record;

/// <summary>
/// Records one authentication/token event on the Audit module's direct-append path (spec §5). Audit owns
/// the routing: the event type, channel, severity and outcome all follow from <see cref="Event"/>, so a
/// caller can't put a sign-in on the wrong channel. Client (ip, user agent) and correlation come from the
/// ambient audit context of the request. Fail-open (I10): the handler never throws, and a failed Result is
/// for diagnostics only — callers must not fail the sign-in or token request on it.
/// </summary>
/// <param name="TenantId">Tenant the event belongs to (the row's partition scope).</param>
/// <param name="Reason">Required for <see cref="SecurityEvent.SignInFailed"/>; the end reason for <see cref="SecurityEvent.SessionEnded"/>.</param>
public sealed record Command(
    SecurityEvent Event,
    Guid TenantId,
    DateTimeOffset OccurredAt,
    Actor Actor,
    IReadOnlyList<Target> Targets,
    OutcomeReason? Reason = null,
    SessionContext? Session = null) : IRequest<Result>;
