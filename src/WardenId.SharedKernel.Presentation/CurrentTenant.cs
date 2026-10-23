using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using WardenId.SharedKernel.Application;

namespace WardenId.SharedKernel.Presentation;

public sealed class CurrentTenant(
    IHttpContextAccessor httpContextAccessor) : ICurrentTenant
{
    public string Slug => (httpContextAccessor.HttpContext?.Request.RouteValues[RouteParameters.Slug] as string)!;

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