using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using WardenId.Protocol.Application.Features.Authorize;
using WardenId.Protocol.Application.Features.Authorize.SignIn;
using WardenId.Protocol.Application.Features.Policies;
using WardenId.Protocol.Domain.Sessions;
using ResolveTenantQuery = WardenId.Protocol.Application.Features.Authorize.ResolveTenant.Query;
using SignInCommand = WardenId.Protocol.Application.Features.Authorize.SignIn.Command;
using EndSessionCommand = WardenId.Protocol.Application.Features.Sessions.End.Command;
using StartSessionCommand = WardenId.Protocol.Application.Features.Sessions.Start.Command;

namespace WardenId.Protocol.Presentation.Pages.Tenants;

public sealed class LoginModel(ISender sender, ITenantLifetimes tenantLifetimes) : PageModel
{
    // Session.Start failing is an operational fault, not the user's credentials: no detail, no cookie.
    private const string SessionStartFailed = "Sign-in failed. Please try again.";

    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    [BindProperty] public string EmailOrUsername { get; set; } = string.Empty;
    [BindProperty] public string Password { get; set; } = string.Empty;

    public string? ErrorMessage { get; private set; }
    public string? BlockedMessage { get; private set; }
    public bool SignedIn { get; private set; }
    public ReturnToApplication? ReturnToApplication { get; private set; }

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken cancellationToken)
    {
        if (await ResolveTenantAsync(slug, cancellationToken) is null) return NotFound();

        ReturnToApplication = BuildReturnToApplication(slug);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string slug, CancellationToken cancellationToken)
    {
        if (await ResolveTenantAsync(slug, cancellationToken) is not { } tenantId) return NotFound();

        ReturnToApplication = BuildReturnToApplication(slug);
        var result = await sender.Send(new SignInCommand(tenantId, EmailOrUsername, Password), cancellationToken);
        Password = string.Empty;

        if (result.IsFailed)
        {
            ErrorMessage = result.Errors[0].Message;
            return Page();
        }

        if (result.Value.BlockedReason is { } reason)
        {
            BlockedMessage = Describe(reason);
            return Page();
        }

        var userId = result.Value.UserId;
        var cookieLifetime = (await tenantLifetimes.GetAsync(tenantId, cancellationToken)).SessionCookie;
        var existing = await TenantSession.ReadAsync(HttpContext, tenantId, sender, cancellationToken);
        if (existing is not null && existing.UserId == userId)
        {
            // Same user already signed in here: keep the one session row, just re-issue the cookie.
            await TenantSession.SignInAsync(HttpContext, tenantId, userId, existing.Sid, existing.AuthTime, cookieLifetime);
        }
        else
        {
            // A different user's session must not be orphaned (live row with no cookie).
            if (existing is not null)
                await sender.Send(
                    new EndSessionCommand(tenantId, existing.Sid, EndReason.UserLogout, RequireExisting: false),
                    cancellationToken);

            var started = await sender.Send(new StartSessionCommand(tenantId, userId), cancellationToken);
            if (started.IsFailed)
            {
                ErrorMessage = SessionStartFailed;
                return Page();
            }

            await TenantSession.SignInAsync(HttpContext, tenantId, userId, started.Value.Sid, started.Value.AuthTime, cookieLifetime);
        }

        if (ReturnToApplication is not null) return LocalRedirect(ReturnUrl!);

        SignedIn = true;
        return Page();
    }

    private async Task<Guid?> ResolveTenantAsync(string slug, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ResolveTenantQuery(slug), cancellationToken);
        return result.IsSuccess ? result.Value : null;
    }

    private ReturnToApplication? BuildReturnToApplication(string slug)
    {
        if (!AuthorizeReturnUrl.IsAllowed(ReturnUrl, slug)) return null;

        var query = QueryHelpers.ParseQuery(new Uri("https://local" + ReturnUrl).Query);
        var parameters = query
            .SelectMany(p => p.Value.Select(v => KeyValuePair.Create(p.Key, v ?? string.Empty)))
            .ToArray();
        return new ReturnToApplication(slug, parameters, "Cancel");
    }

    private static string Describe(SignInBlockReason reason) => reason switch
    {
        SignInBlockReason.VerifyEmail or SignInBlockReason.PendingVerification =>
            "Please verify your email address using the link we sent you, then try again.",
        SignInBlockReason.ResetPassword => "You need to reset your password before you can sign in.",
        SignInBlockReason.AcceptTos => "You need to accept the updated terms of service before you can sign in.",
        SignInBlockReason.EnrollMfa => "You need to set up multi-factor authentication before you can sign in.",
        _ => "Your account needs attention before you can sign in."
    };
}
