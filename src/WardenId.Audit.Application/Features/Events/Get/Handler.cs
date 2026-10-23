using FluentResults;
using MediatR;
using WardenId.Audit.Domain;
using WardenId.Audit.Query.Contracts;
using AuditEventQuery = WardenId.Audit.Query.Contracts.Features.AuditEvents.Get.Query;

namespace WardenId.Audit.Application.Features.Events.Get;

/// <summary>Read side: the row comes from the Dapper query layer (Audit.Query); its payload is decoded here.</summary>
public sealed class Handler(IAuditEventsQuery auditEventsQuery) : IRequestHandler<Query, Result<AuditEventDetail>>
{
    public async Task<Result<AuditEventDetail>> Handle(Query request, CancellationToken cancellationToken)
    {
        var result = await auditEventsQuery.Get(new AuditEventQuery(request.EventId), cancellationToken);
        if (result.IsFailed) return Result.Fail(result.Errors);
        if (result.Value is not { } row) return Result.Fail(AuditErrors.NotFound);

        var channel = AuditEventColumns.ParseChannel(row.Channel);
        var occurredAt = AuditEventColumns.AsUtc(row.OccurredAt);

        var auditEvent = AuditPayloadDecoder.TryDecode(row.Payload);
        if (auditEvent is null)
        {
            return Result.Ok(new AuditEventDetail(
                row.Id, channel, row.TenantId, row.Sequence, occurredAt,
                PayloadAvailable: false,
                row.EventType, row.Actor, row.Targets, AuditEventColumns.ParseOutcome(row.Outcome),
                Envelope: null));
        }

        return Result.Ok(new AuditEventDetail(
            row.Id,
            channel,
            row.TenantId,
            row.Sequence,
            occurredAt,
            PayloadAvailable: true,
            auditEvent.EventType.Key,
            auditEvent.Actor.ToSerializedForm(),
            auditEvent.Targets.Select(t => t.ToSerializedForm()).ToArray(),
            auditEvent.Outcome.Status,
            ToEnvelope(auditEvent)));
    }

    private static AuditEnvelope ToEnvelope(AuditEvent e) => new(
        e.SchemaVersion,
        e.Severity,
        ToActor(e.Actor),
        e.Targets.Select(t => new AuditEnvelopeTarget(t.ToSerializedForm(), t.Type.Key, t.Id, t.CreatedVia?.Key)).ToArray(),
        new AuditEnvelopeOutcome(e.Outcome.Status, e.Outcome.Reason?.Key),
        new AuditEnvelopeClient(e.Client.IpAddress, e.Client.UserAgent, e.Client.Asn, e.Client.GeoLocation),
        new AuditEnvelopeAuthContext(
            e.AuthenticationContext.Sid,
            e.AuthenticationContext.Jti,
            e.AuthenticationContext.Acr,
            e.AuthenticationContext.Amr,
            e.AuthenticationContext.AuthTime,
            e.AuthenticationContext.Protocol),
        new AuditEnvelopeCorrelation(e.Correlation.RequestId, e.Correlation.TraceId, e.Correlation.TransactionId),
        e.Changes.Select(c => new AuditEnvelopeChange(c.PropertyName, ToValue(c.OldValue), ToValue(c.NewValue))).ToArray());

    private static AuditEnvelopeActor ToActor(Actor actor) => new(
        actor.ToSerializedForm(),
        actor.Type,
        actor.Id,
        actor.Scope.IsPlatform,
        actor.Scope.TenantId,
        actor.OnBehalfOf is null ? null : ToActor(actor.OnBehalfOf));

    private static AuditEnvelopeValue? ToValue(AuditValue? value) => value switch
    {
        null => null,
        AuditValue.Plain plain => new AuditEnvelopeValue(false, plain.DisplayValue, null, null),
        AuditValue.Redacted redacted => new AuditEnvelopeValue(true, null, redacted.Value.Hash, redacted.Value.Prefix),
        _ => throw new NotSupportedException($"Unknown audit value kind: {value.GetType()}")
    };
}
