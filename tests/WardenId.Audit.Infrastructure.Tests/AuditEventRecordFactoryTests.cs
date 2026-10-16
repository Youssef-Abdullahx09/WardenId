namespace WardenId.Audit.Infrastructure.Tests;

public class AuditEventRecordFactoryTests
{
    private static readonly Guid TenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static AuditEvent MakeEvent() => AuditEvent.Create(
        Guid.NewGuid(), AuditChannel.AdminConfig, AuditEventType.AdminTenantSuspend, 1,
        DateTimeOffset.Parse("2026-10-16T08:00:00Z"), AuditSeverity.Warning,
        Actor.Create(ActorType.AdminUser, "admin-1", ActorScope.ForTenant(TenantId)).Value,
        AuditOutcome.Failure(AuditOutcomeReason.PolicyDenied),
        ClientContext.Unknown(), AuthContext.None(), Correlation.None(),
        targets:
        [
            AuditTarget.Create(AuditTargetType.Tenant, "tenant-1").Value,
            AuditTarget.Create(AuditTargetType.User, "user-1").Value
        ]).Value;

    [Fact]
    public void Create_StoresTheCanonicalPayloadAndItsLeafHash()
    {
        var auditEvent = MakeEvent();

        var record = AuditEventRecordFactory.Create(auditEvent, TenantId);

        Assert.Equal(auditEvent.Id, record.Id);
        Assert.Equal(nameof(AuditChannel.AdminConfig), record.Channel);
        Assert.Equal(TenantId, record.TenantId);
        Assert.Equal(auditEvent.OccurredAt, record.OccurredAt);
        Assert.Null(record.Sequence);
        Assert.Equal(CanonicalAuditEventSerializer.Serialize(auditEvent), record.Payload);
        Assert.Equal(MerkleHash.Leaf(record.Payload!), record.LeafHash);
    }

    [Fact]
    public void Create_FillsTheSearchColumnsFromTheEvent()
    {
        var auditEvent = MakeEvent();

        var record = AuditEventRecordFactory.Create(auditEvent, TenantId);

        Assert.Equal("admin.tenant.suspend", record.EventType);
        Assert.Equal($"tenant:{TenantId}:adminuser:admin-1", record.Actor);
        Assert.Equal("admin-1", record.ActorId);
        Assert.Equal(["tenant:tenant-1", "user:user-1"], record.Targets);
        Assert.Equal(nameof(AuditResult.Failure), record.Outcome);
    }
}
