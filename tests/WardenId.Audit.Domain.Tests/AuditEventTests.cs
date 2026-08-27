namespace WardenId.Audit.Domain.Tests;

public class AuditEventTests
{
    [Fact]
    public void Create_WithKnownEventType_Succeeds()
    {
        var result = AuditEvent.Create(
            Guid.NewGuid(), AuditChannel.AuthnInteractive, AuditEventType.UserSessionStart, 1, DateTimeOffset.UtcNow,
            AuditSeverity.Info, Actor.Anonymous(ActorScope.Platform()), AuditOutcome.Success(),
            ClientContext.Unknown(), AuthContext.None(), Correlation.None());

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Create_WithNonPositiveSchemaVersion_Fails()
    {
        var result = AuditEvent.Create(
            Guid.NewGuid(), AuditChannel.AuthnInteractive, AuditEventType.UserSessionStart, 0, DateTimeOffset.UtcNow,
            AuditSeverity.Info, Actor.Anonymous(ActorScope.Platform()), AuditOutcome.Success(),
            ClientContext.Unknown(), AuthContext.None(), Correlation.None());

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_WithoutTargetsOrChanges_DefaultsToEmptyArrays_NeverNull()
    {
        var auditEvent = TestAuditEvents.Minimal();

        Assert.Empty(auditEvent.Targets);
        Assert.Empty(auditEvent.Changes);
    }

    [Fact]
    public void FailedLoginWithUnknownUsername_ProducesAnonymousActor()
    {
        var result = AuditEvent.Create(
            Guid.NewGuid(), AuditChannel.AuthnInteractive, AuditEventType.UserLoginFailed, 1, DateTimeOffset.UtcNow,
            AuditSeverity.Warning, Actor.Anonymous(ActorScope.Platform()),
            AuditOutcome.Failure(AuditOutcomeReason.UnknownUsername),
            ClientContext.Unknown(), AuthContext.None(), Correlation.None());

        Assert.True(result.IsSuccess);
        Assert.Equal(ActorType.Anonymous, result.Value.Actor.Type);
    }
}
