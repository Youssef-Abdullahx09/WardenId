namespace WardenId.Audit.Domain.Tests;

public class CanonicalAuditEventSerializerTests
{
    [Fact]
    public void Serialize_SameEventTwice_IsByteIdentical()
    {
        var auditEvent = TestAuditEvents.FullyPopulated();

        var first = CanonicalAuditEventSerializer.Serialize(auditEvent);
        var second = CanonicalAuditEventSerializer.Serialize(auditEvent);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Serialize_TwoSeparatelyConstructedButEqualEvents_AreByteIdentical()
    {
        // Simulates "byte-identical across process restarts" — two independently built events
        // with the same logical content must serialize to the exact same bytes.
        var first = CanonicalAuditEventSerializer.Serialize(TestAuditEvents.FullyPopulated());
        var second = CanonicalAuditEventSerializer.Serialize(TestAuditEvents.FullyPopulated());

        Assert.Equal(first, second);
    }

    [Fact]
    public void RoundTrip_MinimalEvent_ReserializesToIdenticalBytes()
    {
        var original = CanonicalAuditEventSerializer.Serialize(TestAuditEvents.Minimal());

        var rehydrated = CanonicalAuditEventSerializer.Deserialize(original);
        var reserialized = CanonicalAuditEventSerializer.Serialize(rehydrated);

        Assert.Equal(original, reserialized);
    }

    [Fact]
    public void RoundTrip_FullyPopulatedEvent_ReserializesToIdenticalBytes()
    {
        var original = CanonicalAuditEventSerializer.Serialize(TestAuditEvents.FullyPopulated());

        var rehydrated = CanonicalAuditEventSerializer.Deserialize(original);
        var reserialized = CanonicalAuditEventSerializer.Serialize(rehydrated);

        Assert.Equal(original, reserialized);
    }

    [Fact]
    public void RoundTrip_PreservesId()
    {
        var auditEvent = TestAuditEvents.FullyPopulated();

        var rehydrated = CanonicalAuditEventSerializer.Deserialize(CanonicalAuditEventSerializer.Serialize(auditEvent));

        Assert.Equal(auditEvent.Id, rehydrated.Id);
    }

    [Fact]
    public void RoundTrip_PreservesImpersonationChain()
    {
        var auditEvent = TestAuditEvents.FullyPopulated();

        var rehydrated = CanonicalAuditEventSerializer.Deserialize(CanonicalAuditEventSerializer.Serialize(auditEvent));

        Assert.NotNull(rehydrated.Actor.OnBehalfOf);
        Assert.Equal(auditEvent.Actor.OnBehalfOf!.Id, rehydrated.Actor.OnBehalfOf!.Id);
    }

    [Fact]
    public void RoundTrip_PreservesRedactedPropertyChange_WithoutEverSeeingRawValue()
    {
        var auditEvent = TestAuditEvents.FullyPopulated();
        var originalRedacted = (AuditValue.Redacted)auditEvent.Changes.Single(c => c.PropertyName == "ClientSecret").OldValue!;

        var rehydrated = CanonicalAuditEventSerializer.Deserialize(CanonicalAuditEventSerializer.Serialize(auditEvent));
        var rehydratedRedacted = (AuditValue.Redacted)rehydrated.Changes.Single(c => c.PropertyName == "ClientSecret").OldValue!;

        Assert.Equal(originalRedacted.Value.Hash, rehydratedRedacted.Value.Hash);
        Assert.Equal(originalRedacted.Value.Prefix, rehydratedRedacted.Value.Prefix);
    }

    [Fact]
    public void RoundTrip_PreservesOutcomeFailureReason()
    {
        var auditEvent = TestAuditEvents.FullyPopulated();

        var rehydrated = CanonicalAuditEventSerializer.Deserialize(CanonicalAuditEventSerializer.Serialize(auditEvent));

        Assert.Equal(AuditResult.Failure, rehydrated.Outcome.Status);
        Assert.Equal(auditEvent.Outcome.Reason, rehydrated.Outcome.Reason);
    }
}
