namespace WardenId.Audit.Domain;

/// <summary>
/// A physical partition key, not a filter — see docs/audit/audit-module-spec.md §3.3.
/// A flood on AuthnMachine must not degrade queries against AdminConfig.
/// </summary>
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
