using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using EndSessionEndpoint = WardenId.Protocol.Presentation.Features.EndSession.Endpoint;
using ResolveTenantQuery = WardenId.Protocol.Application.Features.Authorize.ResolveTenant.Query;

namespace WardenId.Protocol.Presentation.Pages.Tenants;

/// <summary>
/// RP-Initiated Logout confirmation, shown by /endsession when the request can't prove it is the
/// session's own RP (no valid id_token_hint for this sid). It only renders: Sign out and Cancel post
/// the whitelisted parameters back to /endsession, which re-validates them through OpenIddict and
/// checks antiforgery.
/// </summary>
public sealed class LogoutModel(ISender sender) : PageModel
{
    [BindProperty(SupportsGet = true)] public bool Cancelled { get; set; }

    public string Slug { get; private set; } = string.Empty;
    public IReadOnlyList<KeyValuePair<string, string>> ForwardedParameters { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken cancellationToken)
    {
        if ((await sender.Send(new ResolveTenantQuery(slug), cancellationToken)).IsFailed) return NotFound();

        Slug = slug;
        ForwardedParameters = Request.Query
            .Where(p => EndSessionEndpoint.ForwardedParameters.Contains(p.Key))
            .SelectMany(p => p.Value.Select(v => KeyValuePair.Create(p.Key, v ?? string.Empty)))
            .ToArray();
        return Page();
    }
}
