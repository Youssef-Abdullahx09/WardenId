using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;

namespace WardenId.SharedKernel.Presentation;

public sealed class CurrentUser(
    IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid UserId
    {
        get
        {
            var principal = httpContextAccessor.HttpContext?.User;
            var subject = principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);
            return Guid.TryParse(subject, out var userId) ? userId : Guid.Empty;
        }
    }
}