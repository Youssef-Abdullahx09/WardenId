using MediatR;
using Microsoft.AspNetCore.Mvc;
using WardenId.Protocol.Application.Features.Authorize;
using WardenId.Protocol.Application.Features.Authorize.SignIn;
using WardenId.Protocol.Application.Features.Policies;
using SignInCommand = WardenId.Protocol.Application.Features.Authorize.SignIn.Command;

namespace WardenId.Protocol.Presentation.Pages.Tenants;

public sealed class LoginModel(ISender sender, ITenantLifetimes tenantLifetimes)
    : SignInStepPageModel(sender, tenantLifetimes)
{
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    [BindProperty] public string EmailOrUsername { get; set; } = string.Empty;
    [BindProperty] public string Password { get; set; } = string.Empty;

    public string? BlockedMessage { get; private set; }

    /// <summary>A live session exists already: the page links the account page.</summary>
    public bool AlreadySignedIn { get; private set; }

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken cancellationToken)
    {
        if (await ResolveTenantAsync(slug, cancellationToken) is not { } tenantId) return NotFound();

        UseReturnUrl(ReturnUrl);
        AlreadySignedIn = await TenantSession.ReadAsync(HttpContext, tenantId, Sender, cancellationToken) is not null;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string slug, CancellationToken cancellationToken)
    {
        if (await ResolveTenantAsync(slug, cancellationToken) is not { } tenantId) return NotFound();

        UseReturnUrl(ReturnUrl);
        var result = await Sender.Send(new SignInCommand(tenantId, EmailOrUsername, Password), cancellationToken);
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

        // The password is right but MFA is still to do: no session yet, only the pending ticket.
        if (result.Value.PendingSteps is [var next, ..])
        {
            PendingSignIn.Issue(HttpContext, PendingSignIn.Create(
                HttpContext, tenantId, result.Value.UserId, result.Value.PendingSteps, ContinueUrl,
                AuthenticationMethods.PasswordOnly));
            return LocalRedirect(StepPath(slug, next));
        }

        PendingSignIn.Clear(HttpContext);
        await FinishAsync(tenantId, result.Value.UserId, AuthenticationMethods.PasswordOnly, cancellationToken);
        return AfterSignIn();
    }

    private static string Describe(SignInBlockReason reason) => reason switch
    {
        SignInBlockReason.VerifyEmail or SignInBlockReason.PendingVerification =>
            "Please verify your email address using the link we sent you, then try again.",
        SignInBlockReason.ResetPassword => "You need to reset your password before you can sign in.",
        _ => "Your account needs attention before you can sign in."
    };
}
