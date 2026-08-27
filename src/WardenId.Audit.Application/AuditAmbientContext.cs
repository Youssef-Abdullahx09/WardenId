using WardenId.Audit.Domain;

namespace WardenId.Audit.Application;

/// <summary>
/// TenantId is the ambient tenant scope of the current operation — not necessarily
/// Actor.Scope.TenantId. A platform admin suspending tenant X still produces a tenant-scoped
/// audit row for tenant X; the actor's own scope stays Platform. Null means a platform-scoped
/// operation.
/// </summary>
public sealed record AuditAmbientContext(Actor Actor, Guid? TenantId, Correlation Correlation, ClientContext Client);
