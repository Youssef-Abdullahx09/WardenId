using WardenId.Audit.Application.Features.Events.Get;
using AuditEventRow = WardenId.Audit.Query.Contracts.Features.AuditEvents.Get.Item;

namespace WardenId.Audit.Application.Tests.Features.Events.Get;

// Aliased inside the namespace: WardenId.Audit.Query (the Dapper read side) would otherwise shadow the request type.
using Query = WardenId.Audit.Application.Features.Events.Get.Query;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset OccurredAt = DateTimeOffset.Parse("2026-10-16T08:00:00Z");

    private static AuditEvent UpdateEvent()
    {
        var admin = Actor.Create(ActorType.AdminUser, "admin-1", ActorScope.Platform()).Value;
        return AuditEvent.Create(
            Guid.NewGuid(),
            AuditChannel.AdminDirectory,
            AuditEventType.AdminEntityUpdate,
            1,
            OccurredAt,
            AuditSeverity.Info,
            Actor.Create(ActorType.ServiceAccount, "svc-1", ActorScope.ForTenant(TenantId), onBehalfOf: admin).Value,
            AuditOutcome.Success(),
            ClientContext.Create("203.0.113.7", "curl/8", "AS64500", "NL").Value,
            AuthContext.Create("sid-1", "jti-1", "urn:acr:mfa", ["pwd", "otp"], OccurredAt.AddMinutes(-5), "oidc"),
            Correlation.Create("req-1", "trace-1", Guid.Parse("33333333-3333-3333-3333-333333333333")),
            [AuditTarget.Create(AuditTargetType.User, "user-1", AuditCreationSource.Dcr).Value],
            [
                PropertyChange.Create("Name", new AuditValue.Plain("Old"), new AuditValue.Plain("New")).Value,
                PropertyChange.Create(
                    "Secret",
                    null,
                    new AuditValue.Redacted(ProtectedValue.Redact("super-secret-value"))).Value
            ]).Value;
    }

    private static FakeAuditEventsQuery QueryReturning(Guid id, byte[]? payload) => new()
    {
        GetResponse = Result.Ok<AuditEventRow?>(new AuditEventRow
        {
            Id = id,
            Channel = nameof(AuditChannel.AdminDirectory),
            TenantId = TenantId,
            Sequence = 3,
            OccurredAt = OccurredAt.UtcDateTime,
            Payload = payload,
            EventType = "admin.entity.update",
            Actor = $"tenant:{TenantId}:serviceaccount:svc-1",
            Targets = ["user:user-1"],
            Outcome = nameof(AuditResult.Success)
        })
    };

    [Fact]
    public async Task Handle_UnknownEvent_IsNotFound()
    {
        var result = await new Handler(new FakeAuditEventsQuery()).Handle(new Query(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(AuditErrorCodes.NotFound, result.Errors[0].Metadata[AuditErrorCodes.MetadataKey]);
    }

    [Fact]
    public async Task Handle_DecodesTheWholeEnvelope()
    {
        var auditEvent = UpdateEvent();
        var search = QueryReturning(auditEvent.Id, CanonicalAuditEventSerializer.Serialize(auditEvent));

        var result = await new Handler(search).Handle(new Query(auditEvent.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var detail = result.Value;
        Assert.Equal(auditEvent.Id, detail.Id);
        Assert.Equal(AuditChannel.AdminDirectory, detail.Channel);
        Assert.Equal(TenantId, detail.TenantId);
        Assert.Equal(3, detail.Sequence);
        Assert.Equal(OccurredAt, detail.OccurredAt);
        Assert.True(detail.PayloadAvailable);
        Assert.Equal("admin.entity.update", detail.EventType);
        Assert.Equal(auditEvent.Actor.ToSerializedForm(), detail.Actor);
        Assert.Equal(["user:user-1"], detail.Targets);
        Assert.Equal(AuditResult.Success, detail.Result);

        var envelope = Assert.IsType<AuditEnvelope>(detail.Envelope);
        Assert.Equal(1, envelope.SchemaVersion);
        Assert.Equal(AuditSeverity.Info, envelope.Severity);

        Assert.Equal(auditEvent.Actor.ToSerializedForm(), envelope.Actor.Key);
        Assert.Equal(ActorType.ServiceAccount, envelope.Actor.Type);
        Assert.Equal("svc-1", envelope.Actor.Id);
        Assert.False(envelope.Actor.IsPlatform);
        Assert.Equal(TenantId, envelope.Actor.TenantId);
        Assert.NotNull(envelope.Actor.OnBehalfOf);
        Assert.Equal("platform:adminuser:admin-1", envelope.Actor.OnBehalfOf.Key);
        Assert.True(envelope.Actor.OnBehalfOf.IsPlatform);

        var target = Assert.Single(envelope.Targets);
        Assert.Equal(new AuditEnvelopeTarget("user:user-1", "user", "user-1", "dcr"), target);

        Assert.Equal(new AuditEnvelopeOutcome(AuditResult.Success, null), envelope.Outcome);
        Assert.Equal(new AuditEnvelopeClient("203.0.113.7", "curl/8", "AS64500", "NL"), envelope.Client);

        Assert.Equal("sid-1", envelope.AuthenticationContext.Sid);
        Assert.Equal("jti-1", envelope.AuthenticationContext.Jti);
        Assert.Equal("urn:acr:mfa", envelope.AuthenticationContext.Acr);
        Assert.Equal(["pwd", "otp"], envelope.AuthenticationContext.Amr);
        Assert.Equal(OccurredAt.AddMinutes(-5), envelope.AuthenticationContext.AuthTime);
        Assert.Equal("oidc", envelope.AuthenticationContext.Protocol);

        Assert.Equal(
            new AuditEnvelopeCorrelation("req-1", "trace-1", Guid.Parse("33333333-3333-3333-3333-333333333333")),
            envelope.Correlation);

        Assert.Equal(2, envelope.Changes.Count);
        var name = envelope.Changes[0];
        Assert.Equal("Name", name.PropertyName);
        Assert.Equal(new AuditEnvelopeValue(false, "Old", null, null), name.Old);
        Assert.Equal(new AuditEnvelopeValue(false, "New", null, null), name.New);
    }

    [Fact]
    public async Task Handle_KeepsRedactedValuesRedacted()
    {
        var auditEvent = UpdateEvent();
        var search = QueryReturning(auditEvent.Id, CanonicalAuditEventSerializer.Serialize(auditEvent));

        var result = await new Handler(search).Handle(new Query(auditEvent.Id), CancellationToken.None);

        var secret = result.Value.Envelope!.Changes.Single(c => c.PropertyName == "Secret");
        var expected = ProtectedValue.Redact("super-secret-value");
        Assert.Null(secret.Old);
        Assert.Equal(new AuditEnvelopeValue(true, null, expected.Hash, expected.Prefix), secret.New);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handle_WithoutAReadablePayload_ReturnsTheIndexedSummary(bool corrupt)
    {
        var id = Guid.NewGuid();
        var search = QueryReturning(id, corrupt ? "{\"not\":\"an event\"}"u8.ToArray() : null);

        var result = await new Handler(search).Handle(new Query(id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var detail = result.Value;
        Assert.False(detail.PayloadAvailable);
        Assert.Null(detail.Envelope);
        Assert.Equal("admin.entity.update", detail.EventType);
        Assert.Equal($"tenant:{TenantId}:serviceaccount:svc-1", detail.Actor);
        Assert.Equal(["user:user-1"], detail.Targets);
        Assert.Equal(AuditResult.Success, detail.Result);
        Assert.Equal(3, detail.Sequence);
        Assert.Equal(AuditChannel.AdminDirectory, detail.Channel);
        Assert.Equal(OccurredAt, detail.OccurredAt);
    }

    [Fact]
    public async Task Handle_AsksForTheRequestedEvent()
    {
        var id = Guid.NewGuid();
        var search = new FakeAuditEventsQuery();

        await new Handler(search).Handle(new Query(id), CancellationToken.None);

        Assert.Equal(id, Assert.Single(search.GetCalls).EventId);
    }

    [Fact]
    public async Task Handle_PassesAQueryFailureThrough()
    {
        var search = new FakeAuditEventsQuery { GetResponse = Result.Fail<AuditEventRow?>("boom") };

        var result = await new Handler(search).Handle(new Query(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal("boom", result.Errors[0].Message);
    }
}
