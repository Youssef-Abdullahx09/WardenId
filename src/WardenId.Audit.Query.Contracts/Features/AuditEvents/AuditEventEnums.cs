namespace WardenId.Audit.Query.Contracts.Features.AuditEvents;

// Read-side mirrors of WardenId.Audit.Domain.AuditChannel and AuditResult. Query.Contracts must not depend on Domain, so
// the read model declares its own copies; member names must match the domain enums' (they're what's persisted and
// parsed), and values match too so the two convert by cast.

public enum AuditChannel
{
    AuthnInteractive = 1,
    AuthnSilent = 2,
    AuthnMachine = 3,
    AdminDirectory = 4,
    AdminConfig = 5,
    TokenOps = 6,
    Platform = 7
}

public enum AuditResult
{
    Success = 1,
    Failure = 2
}
