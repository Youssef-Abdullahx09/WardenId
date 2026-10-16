using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using WardenId.SharedKernel.Application;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace WardenId.Gateway.Authorization;

/// <summary>The caller's access token belongs to a user in <see cref="PlatformAdmins"/>.</summary>
public sealed class PlatformAdminRequirement : IAuthorizationRequirement;

public sealed class PlatformAdminHandler(PlatformAdmins admins) : AuthorizationHandler<PlatformAdminRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PlatformAdminRequirement requirement)
    {
        if (Guid.TryParse(context.User.FindFirstValue(CustomClaimTypes.TenantId), out var tenantId)
            && Guid.TryParse(context.User.FindFirstValue(Claims.Subject), out var userId)
            && admins.Contains(tenantId, userId))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
