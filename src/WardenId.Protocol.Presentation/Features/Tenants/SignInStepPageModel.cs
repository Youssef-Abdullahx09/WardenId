using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WardenId.Protocol.Application;
using WardenId.Protocol.Application.Features.Authorize;
using WardenId.Protocol.Application.Features.Authorize.SignIn;
using WardenId.Protocol.Application.Features.Policies;
using ResolveTenantQuery = WardenId.Protocol.Application.Features.Authorize.ResolveTenant.Query;

namespace WardenId.Protocol.Presentation.Features.Tenants;

/// <summary>
/// Shared plumbing of the sign-in pages: tenant lookup, the pending MFA ticket, and finishing the same way
/// everywhere (session via <see cref="TenantSession.EstablishAsync"/>, then back to the authorize request).
/// </summary>
public abstract class SignInStepPageModel(ISender sender, ITenantLifetimes tenantLifetimes) : PageModel
{
    // Session.Start failing is an operational fault, not the user's credentials: no detail, no cookie.
    protected const string SessionStartFailed = "Sign-in failed. Please try again.";

    protected ISender Sender { get; } = sender;

    public string Slug { get; private set; } = string.Empty;

    /// <summary>The tenant's display name (the slug when it has none), set with <see cref="Slug"/>.</summary>
    protected string TenantDisplayName { get; private set; } = string.Empty;
    public string? ErrorMessage { get; protected set; }

    /// <summary>The session exists. <see cref="ContinueUrl"/> is where to go next, if anywhere.</summary>
    public bool SignedIn { get; private set; }

    /// <summary>The authorize request or account page to go to (already checked against this tenant), or null.</summary>
    public string? ContinueUrl { get; private set; }

    public ReturnToApplication? ReturnToApplication { get; protected set; }

    /// <summary>The login page, keeping the authorize request, to start again.</summary>
    public string StartOverUrl => ContinueUrl is null
        ? TenantPages.Login(Slug)
        : TenantPages.Login(Slug, ContinueUrl);

    /// <summary>The account page, linked once the user is signed in.</summary>
    public string AccountUrl => TenantPages.Account(Slug);

    public static string StepPath(string slug, SignInStep step) => step switch
    {
        SignInStep.SecondFactor => ProtocolPaths.Tenant(slug, ProtocolPaths.Pages.LoginCode),
        SignInStep.AcceptTerms => ProtocolPaths.Tenant(slug, ProtocolPaths.Pages.LoginTerms),
        _ => ProtocolPaths.Tenant(slug, ProtocolPaths.Pages.LoginEnrollTotp)
    };

    protected async Task<Guid?> ResolveTenantAsync(string slug, CancellationToken cancellationToken)
    {
        Slug = slug;
        var result = await Sender.Send(new ResolveTenantQuery(slug), cancellationToken);
        if (result.IsFailed) return null;

        TenantDisplayName = result.Value.DisplayName;
        return result.Value.TenantId;
    }

    /// <summary>
    /// Sets the return URL (only this tenant's authorize endpoint or account pages) and, for an authorize request,
    /// the Cancel button.
    /// </summary>
    protected void UseReturnUrl(string? returnUrl)
    {
        ContinueUrl = AuthorizeReturnUrl.IsAllowed(returnUrl, Slug) ? returnUrl : null;
        ReturnToApplication = ReturnToApplication.For(ContinueUrl, Slug, "Cancel");
    }

    /// <summary>
    /// The ticket when its next step is <paramref name="step"/>; otherwise where to go instead (404 for an unknown
    /// tenant, the right step page, or the login page when there's no live ticket).
    /// </summary>
    protected async Task<(PendingSignInTicket? Ticket, IActionResult? Elsewhere)> LoadTicketAsync(
        string slug, SignInStep step, CancellationToken cancellationToken)
    {
        if (await ResolveTenantAsync(slug, cancellationToken) is not { } tenantId) return (null, NotFound());

        var ticket = PendingSignIn.Read(HttpContext, tenantId);
        if (ticket?.NextStep is not { } next)
        {
            PendingSignIn.Clear(HttpContext);
            return (null, LocalRedirect(TenantPages.Login(slug)));
        }

        if (next != step) return (null, LocalRedirect(StepPath(slug, next)));

        UseReturnUrl(ticket.ReturnUrl);
        return (ticket, null);
    }

    /// <summary>
    /// Moves on after the current step: a redirect to the next step's page, or — with none left — null once the
    /// session is attempted (<see cref="SignedIn"/> or <see cref="ErrorMessage"/> tells which).
    /// </summary>
    protected async Task<IActionResult?> ContinueAsync(
        PendingSignInTicket ticket, IReadOnlyList<string> amr, CancellationToken cancellationToken)
    {
        var advanced = PendingSignIn.Advance(HttpContext, ticket, amr);
        if (advanced.NextStep is { } next)
        {
            PendingSignIn.Issue(HttpContext, advanced);
            return LocalRedirect(StepPath(Slug, next));
        }

        PendingSignIn.Clear(HttpContext);
        await FinishAsync(ticket.TenantId, ticket.UserId, amr, cancellationToken);
        return null;
    }

    /// <summary>Starts (or keeps) the session; sets <see cref="SignedIn"/>, or <see cref="ErrorMessage"/> on failure.</summary>
    protected async Task FinishAsync(Guid tenantId, Guid userId, IReadOnlyList<string> amr, CancellationToken cancellationToken)
    {
        if (await TenantSession.EstablishAsync(HttpContext, tenantId, userId, amr, Sender, tenantLifetimes, cancellationToken))
            SignedIn = true;
        else
            ErrorMessage = SessionStartFailed;
    }

    /// <summary>After a finished sign-in: back to the authorize request, or the page itself ("you are signed in").</summary>
    protected IActionResult AfterSignIn() => SignedIn && ContinueUrl is not null ? LocalRedirect(ContinueUrl) : Page();
}
