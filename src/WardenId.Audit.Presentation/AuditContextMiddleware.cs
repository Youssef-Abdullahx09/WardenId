using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;
using WardenId.Audit.Application;
using WardenId.Audit.Domain;
using WardenId.SharedKernel.Application.Utilities;
using WardenId.SharedKernel.Presentation;

namespace WardenId.Audit.Presentation;

/// <summary>
/// Populates <see cref="IAuditContextAccessor"/> for the lifetime of the request from the
/// same claims/route values <see cref="ICurrentUser"/>/<see cref="ICurrentTenant"/> already
/// read — the only HTTP-side producer of ambient audit context (I7). CAP consumers and
/// hosted services populate it separately; see docs/audit/PLAN.md §3.4.
/// </summary>
public sealed class AuditContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAuditContextAccessor auditContextAccessor)
    {
        using (auditContextAccessor.BeginScope(BuildContext(context)))
        {
            await next(context);
        }
    }

    private static AuditAmbientContext BuildContext(HttpContext context)
    {
        var principal = context.User;
        var subject = principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var tenantIdClaim = principal?.FindFirstValue(CustomClaimTypes.TenantId);
        var scope = Guid.TryParse(tenantIdClaim, out var claimTenantId)
            ? ActorScope.ForTenant(claimTenantId)
            : ActorScope.Platform();

        var actor = string.IsNullOrWhiteSpace(subject)
            ? Actor.Anonymous(scope)
            : Actor.Create(ActorType.AuthenticatedUnknown, subject, scope).Value;

        var correlation = Correlation.Create(
            requestId: context.TraceIdentifier,
            traceId: System.Diagnostics.Activity.Current?.Id);

        var userAgent = context.Request.Headers.UserAgent.ToString();
        var clientContext = ClientContext.Create(
                ipAddress: context.Connection.RemoteIpAddress?.ToString(),
                userAgent: string.IsNullOrEmpty(userAgent) ? null : userAgent)
            .ValueOrDefault
            ?? ClientContext.Unknown();

        return new AuditAmbientContext(actor, scope.TenantId, correlation, clientContext);
    }
}
