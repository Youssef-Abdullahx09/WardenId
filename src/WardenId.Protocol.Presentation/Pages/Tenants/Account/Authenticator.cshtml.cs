using MediatR;
using Microsoft.AspNetCore.Mvc;
using BeginTotpEnrollmentCommand = WardenId.Protocol.Application.Features.Authorize.BeginTotpEnrollment.Command;

namespace WardenId.Protocol.Presentation.Pages.Tenants.Account;

/// <summary>
/// Sets up a first authenticator app (no enabled factor, tenant MFA policy not Off). Starting is a POST, so a link
/// can't start an enrollment.
/// </summary>
public sealed class AuthenticatorModel(ISender sender) : TotpEnrollmentPageModel(sender)
{
    protected override AccountEnrollmentKind Kind => AccountEnrollmentKind.Setup;

    private bool CanSetUp => !Account.HasMfa && Account.MfaAvailable;

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken cancellationToken)
    {
        if (await LoadAsync(slug, cancellationToken) is { } elsewhere) return elsewhere;
        return CanSetUp ? Page() : ToOverview();
    }

    public async Task<IActionResult> OnPostStartAsync(string slug, CancellationToken cancellationToken)
    {
        if (await LoadAsync(slug, cancellationToken) is { } elsewhere) return elsewhere;
        if (!CanSetUp) return ToOverview();

        // Issuer: the slug, as at sign-in.
        var started = await Sender.Send(new BeginTotpEnrollmentCommand(TenantId, UserId, slug), cancellationToken);
        if (started.IsFailed)
        {
            ErrorMessage = "We couldn't start setting up your authenticator app. Please try again.";
            return Page();
        }

        return ShowEnrollment(new PendingTotpEnrollment(started.Value.FactorId, started.Value.Secret, started.Value.OtpAuthUri));
    }
}
