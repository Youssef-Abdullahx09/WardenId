using MediatR;
using Microsoft.AspNetCore.Mvc;
using RemoveMfaCommand = WardenId.Protocol.Application.Features.Account.RemoveMfa.Command;

namespace WardenId.Protocol.Presentation.Pages.Tenants.Account;

/// <summary>
/// Turns two-step verification off: a current code (app or recovery code) and an explicit confirmation. Only
/// offered when the tenant's policy doesn't require MFA; Identity refuses it otherwise anyway. The user's other
/// sessions end; this one stays.
/// </summary>
public sealed class RemoveMfaModel(ISender sender) : AccountPageModel(sender)
{
    [BindProperty] public string Code { get; set; } = string.Empty;
    [BindProperty] public bool Confirm { get; set; }

    public bool Removed { get; private set; }

    private bool CanRemove => Account.HasMfa && !Account.MfaRequired;

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken cancellationToken)
    {
        if (await LoadAsync(slug, cancellationToken) is { } elsewhere) return elsewhere;
        return CanRemove ? Page() : ToOverview();
    }

    public async Task<IActionResult> OnPostAsync(string slug, CancellationToken cancellationToken)
    {
        if (await LoadAsync(slug, cancellationToken) is { } elsewhere) return elsewhere;
        if (!CanRemove) return ToOverview();

        var code = Code.Trim();
        Code = string.Empty;
        if (!Confirm)
        {
            ErrorMessage = "Please confirm that you want to turn off two-step verification.";
            return Page();
        }

        var result = await Sender.Send(new RemoveMfaCommand(TenantId, UserId, Sid, code), cancellationToken);
        if (result.IsFailed)
        {
            ErrorMessage = result.Errors[0].Message;
            return Page();
        }

        Removed = true;
        return Page();
    }
}
