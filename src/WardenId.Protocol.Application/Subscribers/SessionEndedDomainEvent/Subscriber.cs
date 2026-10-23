using WardenId.Audit.Integration.Command.Features.Events.Record;
using WardenId.Protocol.Application.Auditing;
using WardenId.Protocol.Application.Features.BackchannelLogout;
using WardenId.Protocol.Domain.Sessions;
using WardenId.SharedKernel.Application.CapPublisher;
using RecordAuditEventCommand = WardenId.Audit.Integration.Command.Features.Events.Record.Command;
using Requested = WardenId.Protocol.Application.Features.BackchannelLogout.BackchannelLogoutRequested;

namespace WardenId.Protocol.Application.Subscribers.SessionEndedDomainEvent;

/// <summary>
/// Protocol consuming its own SessionEndedDomainEvent, the first hop of the back-channel fan-out (spec D5/D8).
/// <list type="number">
/// <item>Revokes the session's grants that lack offline_access. Offline grants are meant to outlive the browser session.</item>
/// <item>Queues one delivery per participating client. Clients holding only offline grants are still told the session ended.</item>
/// <item>Audits the session end (one place for every end reason). Last, so a message CAP retries after a failed
/// fan-out is audited once; the audit write itself never throws.</item>
/// </list>
/// Revocation runs first: if it throws, CAP retries the whole message before any RP is told.
/// </summary>
public sealed class Subscriber(
    ISessionAuthorizationRevoker revoker,
    IBackchannelLogoutQueue queue,
    IAuthAuditTrail audit,
    TimeProvider timeProvider) : ICapSubscriber<Message>
{
    // The audit actor: the session end is carried out by Protocol, whoever asked for it (the reason says why).
    private const string AuditActor = "protocol";

    // Group is explicit: CAP silently drops one of two subscriptions sharing (topic, group).
    [CapSubscriber(nameof(SessionEndedDomainEvent), Group = SubscriberGroups.Prefix + nameof(SessionEndedDomainEvent))]
    public async Task OnReceive(Message message)
    {
        var revocable = message.Authorizations
            .Where(authorization => !authorization.OfflineAccess)
            .Select(authorization => authorization.AuthorizationId)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (revocable.Count > 0)
            await revoker.RevokeAsync(revocable, CancellationToken.None);

        var clientIds = message.Authorizations
            .Select(authorization => authorization.ClientId)
            .Distinct(StringComparer.Ordinal);
        foreach (var clientId in clientIds)
        {
            await queue.EnqueueAsync(
                new Requested(message.TenantId, clientId, message.Sid, message.SubjectId),
                CancellationToken.None);
        }

        await audit.RecordAsync(new RecordAuditEventCommand(
            SecurityEvent.SessionEnded,
            message.TenantId,
            message.OccurredOnUtc == default ? timeProvider.GetUtcNow() : message.OccurredOnUtc,
            Actor.System(AuditActor),
            [Target.User(message.SubjectId), Target.Session(message.Sid)],
            ToOutcomeReason(message.Reason),
            new SessionContext(message.Sid)));
    }

    // Never throws: this runs after the fan-out, and a throw would make CAP redeliver it.
    private static OutcomeReason? ToOutcomeReason(EndReason reason) => reason switch
    {
        EndReason.UserLogout => OutcomeReason.UserLogout,
        EndReason.PasswordChanged => OutcomeReason.PasswordChanged,
        EndReason.UserDisabled => OutcomeReason.UserDisabled,
        EndReason.UserDeleted => OutcomeReason.UserDeleted,
        EndReason.AdminEnded => OutcomeReason.AdminEnded,
        EndReason.AccountLocked => OutcomeReason.AccountLocked,
        EndReason.MfaChanged => OutcomeReason.MfaChanged,
        _ => null
    };
}
