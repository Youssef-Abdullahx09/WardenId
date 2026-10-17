using MediatR;
using Microsoft.AspNetCore.Mvc;
using BeginTotpReplacementCommand = WardenId.Protocol.Application.Features.Account.BeginTotpReplacement.Command;

namespace WardenId.Protocol.Presentation.Pages.Tenants.Account;

/// <summary>
/// Replaces the authenticator app: a current code first (app or recovery code), then the same setup as a first
/// app. The old app keeps working until the new one is confirmed; then the user's other sessions end and this one
/// stays.
/// </summary>
public sealed class ReplaceAuthenticatorModel(ISender sender) : TotpEnrollmentPageModel(sender)
{
    protected override AccountEnrollmentKind Kind => AccountEnrollmentKind.Replacement;

    [BindProperty] public string CurrentCode { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken cancellationToken)
    {
        if (await LoadAsync(slug, cancellationToken) is { } elsewhere) return elsewhere;
        return Account.HasMfa ? Page() : ToOverview();
    }

    public async Task<IActionResult> OnPostStartAsync(string slug, CancellationToken cancellationToken)
    {
        if (await LoadAsync(slug, cancellationToken) is { } elsewhere) return elsewhere;
        if (!Account.HasMfa) return ToOverview();

        var code = CurrentCode.Trim();
        CurrentCode = string.Empty;
        var started = await Sender.Send(new BeginTotpReplacementCommand(TenantId, UserId, Sid, code, slug), cancellationToken);
        if (started.IsFailed)
        {
            ErrorMessage = WrongCode;
            return Page();
        }

        return ShowEnrollment(new PendingTotpEnrollment(started.Value.FactorId, started.Value.Secret, started.Value.OtpAuthUri));
    }
}
