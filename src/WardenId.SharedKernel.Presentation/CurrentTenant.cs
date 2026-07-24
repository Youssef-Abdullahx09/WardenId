using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace WardenId.SharedKernel.Presentation;

public sealed class CurrentTenant(
    IHttpContextAccessor httpContextAccessor) : ICurrentTenant
{
    public string Slug => (httpContextAccessor.HttpContext?.Request.RouteValues["slug"] as string)!;

    public Guid TenantId
    {
        get
        {
            var principal = httpContextAccessor.HttpContext?.User;
            var tenant = principal?.FindFirstValue(CustomClaimTypes.TenantId);
            return Guid.TryParse(tenant, out var tenantId) ? tenantId : Guid.Empty;
        }
    }
}