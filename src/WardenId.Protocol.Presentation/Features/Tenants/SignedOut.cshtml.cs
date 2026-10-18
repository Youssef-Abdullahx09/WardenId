using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ResolveTenantQuery = WardenId.Protocol.Application.Features.Authorize.ResolveTenant.Query;

namespace WardenId.Protocol.Presentation.Features.Tenants;

/// <summary>Where /endsession lands when the RP sent no post_logout_redirect_uri (A6).</summary>
public sealed class SignedOutModel(ISender sender) : PageModel
{
    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken cancellationToken) =>
        (await sender.Send(new ResolveTenantQuery(slug), cancellationToken)).IsFailed ? NotFound() : Page();
}
