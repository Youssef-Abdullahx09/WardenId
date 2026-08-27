namespace WardenId.Audit.Domain.Tests;

/// <summary>Builds fully-populated AuditEvents for tests — every field non-default so a bug in any one is visible.</summary>
internal static class TestAuditEvents
{
    public static Actor AdminActor() =>
        Actor.Create(ActorType.AdminUser, "admin-1", ActorScope.ForTenant(Guid.Parse("11111111-1111-1111-1111-111111111111"))).Value;

    public static Actor ImpersonatingActor() =>
        Actor.Create(ActorType.AdminUser, "admin-2", ActorScope.Platform(), onBehalfOf: AdminActor()).Value;

    public static AuditEvent Minimal() =>
        AuditEvent.Create(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            AuditChannel.AuthnInteractive,
            AuditEventType.UserSessionStart,
            1,
            DateTimeOffset.Parse("2026-08-20T12:00:00Z"),
            AuditSeverity.Info,
            Actor.Anonymous(ActorScope.Platform()),
            AuditOutcome.Success(),
            ClientContext.Unknown(),
            AuthContext.None(),
            Correlation.None()).Value;

    public static AuditEvent FullyPopulated() =>
        AuditEvent.Create(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            AuditChannel.AdminConfig,
            AuditEventType.AdminTenantSuspend,
            1,
            DateTimeOffset.Parse("2026-08-20T12:34:56.789Z"),
            AuditSeverity.Warning,
            ImpersonatingActor(),
            AuditOutcome.Failure(AuditOutcomeReason.PolicyDenied),
            ClientContext.Create("203.0.113.7", "curl/8.0", "AS64512", "US-CA").Value,
            AuthContext.Create("sid-1", "jti-1", "urn:mace:incommon:iap:silver", ["pwd", "otp"], DateTimeOffset.Parse("2026-08-20T11:59:00Z"), "oidc"),
            Correlation.Create("req-1", "trace-1", Guid.Parse("44444444-4444-4444-4444-444444444444")),
            [AuditTarget.Create(AuditTargetType.Tenant, "tenant-1", AuditCreationSource.Dcr).Value],
            [
                PropertyChange.Create("Status", new AuditValue.Plain("Active"), new AuditValue.Plain("Suspended")).Value,
                PropertyChange.Create("ClientSecret", new AuditValue.Redacted(ProtectedValue.Redact("super-secret-value")), null).Value
            ]).Value;
}
