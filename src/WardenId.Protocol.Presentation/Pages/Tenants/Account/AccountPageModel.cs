using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WardenId.Protocol.Application.Features.Account.Get;
using WardenId.Protocol.Application.Features.Authorize;
using GetAccountQuery = WardenId.Protocol.Application.Features.Account.Get.Query;
using ResolveTenantQuery = WardenId.Protocol.Application.Features.Authorize.ResolveTenant.Query;

namespace WardenId.Protocol.Presentation.Pages.Tenants.Account;

/// <summary>
/// Shared plumbing of the self-service account pages: they need a live tenant session (<see cref="TenantSession"/>),
/// and the user is always the session's — never anything from the request. Without a session the user goes to the
/// login page, which comes back here afterwards.
/// </summary>
public abstract class AccountPageModel(ISender sender) : PageModel
{
    // One answer for a code the user typed, whatever Identity's reason; the step-up never says more.
    protected const string WrongCode = "That code didn't work. Please try again.";

    protected ISender Sender { get; } = sender;

    protected Guid TenantId { get; private set; }

    /// <summary>The tenant's display name (the slug when it has none): the issuer authenticator apps show.</summary>
    protected string TenantDisplayName { get; private set; } = string.Empty;
    protected Guid UserId { get; private set; }

    /// <summary>The live login session the page runs under: audited with each change, and kept when others end.</summary>
    protected Guid Sid { get; private set; }

    public string Slug { get; private set; } = string.Empty;
    public string? ErrorMessage { get; protected set; }

    /// <summary>Set by <see cref="LoadAsync"/> before any handler continues.</summary>
    public AccountSummary Account { get; private set; } = null!;

    public string AccountUrl => $"/tenants/{Slug}/account";

    /// <summary>The login page, coming back to the account page.</summary>
    public string SignInUrl => $"/tenants/{Slug}/login?ReturnUrl={Uri.EscapeDataString(AccountUrl)}";

    /// <summary>
    /// Null when the page may go on with <see cref="Account"/> loaded; otherwise where to go instead: 404 for an
    /// unknown tenant, the login page without a live session for this tenant.
    /// </summary>
    protected async Task<IActionResult?> LoadAsync(string slug, CancellationToken cancellationToken)
    {
        Slug = slug;
        var tenant = await Sender.Send(new ResolveTenantQuery(slug), cancellationToken);
        if (tenant.IsFailed) return NotFound();
        TenantId = tenant.Value.TenantId;
        TenantDisplayName = tenant.Value.DisplayName;

        var session = await TenantSession.ReadAsync(HttpContext, TenantId, Sender, cancellationToken);
        if (session is null) return ToLogin();
        UserId = session.UserId;
        Sid = session.Sid;

        var account = await Sender.Send(new GetAccountQuery(TenantId, UserId), cancellationToken);
        if (account.IsFailed)
        {
            // The session's user is gone for Identity: drop the cookie rather than loop.
            await HttpContext.SignOutAsync(TenantSession.Scheme);
            return ToLogin();
        }

        Account = account.Value;
        return null;
    }

    /// <summary>Back to the overview: for pages whose action doesn't apply to this account.</summary>
    protected IActionResult ToOverview() => LocalRedirect(AccountUrl);

    private IActionResult ToLogin()
    {
        var back = AuthorizeReturnUrl.IsAccountPage(Request.Path.Value, Slug) ? Request.Path.Value! : AccountUrl;
        return LocalRedirect($"/tenants/{Slug}/login?ReturnUrl={Uri.EscapeDataString(back)}");
    }
}
