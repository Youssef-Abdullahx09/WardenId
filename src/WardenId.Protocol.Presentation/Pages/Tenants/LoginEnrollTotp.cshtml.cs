using MediatR;
using Microsoft.AspNetCore.Mvc;
using WardenId.Protocol.Application.Features.Authorize;
using WardenId.Protocol.Application.Features.Authorize.SignIn;
using WardenId.Protocol.Application.Features.Policies;
using BeginTotpEnrollmentCommand = WardenId.Protocol.Application.Features.Authorize.BeginTotpEnrollment.Command;
using CompleteTotpEnrollmentCommand = WardenId.Protocol.Application.Features.Authorize.CompleteTotpEnrollment.Command;

namespace WardenId.Protocol.Presentation.Pages.Tenants;

/// <summary>
/// Sets up an authenticator app during sign-in: QR code and secret, the first code, then the recovery codes,
/// shown once. The secret and factor id stay in the protected pending ticket, so a wrong code re-renders the
/// same QR code without the secret ever being posted back.
/// </summary>
public sealed class LoginEnrollTotpModel(ISender sender, ITenantLifetimes tenantLifetimes)
    : SignInStepPageModel(sender, tenantLifetimes)
{
    private const string EnrollmentFailed = "We couldn't start setting up your authenticator app. Please start over.";

    [BindProperty] public string Code { get; set; } = string.Empty;

    public PendingTotpEnrollment? Enrollment { get; private set; }

    /// <summary>Set once the factor is enabled: shown on this response only.</summary>
    public IReadOnlyList<string>? RecoveryCodes { get; private set; }

    public string QrCodeSvgMarkup => Enrollment is null
        ? string.Empty
        : QrCodeSvg.Render(Enrollment.OtpAuthUri, "QR code to scan with your authenticator app");

    /// <summary>The secret in groups of four, easier to type into an app by hand.</summary>
    public string GroupedSecret => Enrollment is null
        ? string.Empty
        : string.Join(' ', Enrollment.Secret.Chunk(4).Select(c => new string(c)));

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken cancellationToken)
    {
        var (ticket, elsewhere) = await LoadTicketAsync(slug, SignInStep.EnrollTotp, cancellationToken);
        if (ticket is null) return elsewhere!;

        if (ticket.Enrollment is null)
        {
            // Issuer: the slug — Protocol has no cheap way to the tenant's display name.
            var started = await Sender.Send(
                new BeginTotpEnrollmentCommand(ticket.TenantId, ticket.UserId, slug), cancellationToken);
            if (started.IsFailed)
            {
                ErrorMessage = EnrollmentFailed;
                return Page();
            }

            ticket = PendingSignIn.WithEnrollment(HttpContext, ticket,
                new PendingTotpEnrollment(started.Value.FactorId, started.Value.Secret, started.Value.OtpAuthUri));
            PendingSignIn.Issue(HttpContext, ticket);
        }

        Enrollment = ticket.Enrollment;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string slug, CancellationToken cancellationToken)
    {
        var (ticket, elsewhere) = await LoadTicketAsync(slug, SignInStep.EnrollTotp, cancellationToken);
        if (ticket is null) return elsewhere!;
        if (ticket.Enrollment is not { } enrollment) return LocalRedirect(StepPath(slug, SignInStep.EnrollTotp));

        var result = await Sender.Send(
            new CompleteTotpEnrollmentCommand(ticket.TenantId, ticket.UserId, enrollment.FactorId, Code.Trim()),
            cancellationToken);
        Code = string.Empty;
        if (result.IsFailed)
        {
            ErrorMessage = result.Errors[0].Message;
            Enrollment = enrollment;
            return Page();
        }

        // The factor is on and the old recovery codes are gone: show the new ones even if the session fails.
        RecoveryCodes = result.Value.RecoveryCodes;
        return await ContinueAsync(ticket, AuthenticationMethods.PasswordAndTotp, cancellationToken) ?? Page();
    }
}
