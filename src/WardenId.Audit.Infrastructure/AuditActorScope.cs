using WardenId.Audit.Application;
using WardenId.Audit.Domain;
using WardenId.SharedKernel.Application.Abstractions;

namespace WardenId.Audit.Infrastructure;

/// <summary>
/// Replaces the ambient actor and tenant for the scope, keeping the request's client and correlation (or unknown/none
/// outside a request). Restores the previous context on dispose.
/// </summary>
public sealed class AuditActorScope(IAuditContextAccessor auditContextAccessor) : IAuditActorScope
{
    public IDisposable BeginEndUser(Guid tenantId, Guid userId)
    {
        var current = auditContextAccessor.Current;
        var actor = Actor.Create(ActorType.EndUser, userId.ToString(), ActorScope.ForTenant(tenantId)).Value;

        return auditContextAccessor.BeginScope(new AuditAmbientContext(
            actor,
            tenantId,
            current?.Correlation ?? Correlation.None(),
            current?.Client ?? ClientContext.Unknown()));
    }
}
