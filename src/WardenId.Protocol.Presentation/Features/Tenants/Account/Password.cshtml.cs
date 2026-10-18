using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using ChangePasswordCommand = WardenId.Protocol.Application.Features.Account.ChangePassword.Command;

namespace WardenId.Protocol.Presentation.Features.Tenants.Account;

/// <summary>
/// Changes the password after checking the current one. Identity ends the user's sessions on success, so this
/// browser's cookie is dropped as well and the user signs in again.
/// </summary>
public sealed class PasswordModel(ISender sender) : AccountPageModel(sender)
{
    [BindProperty] public string CurrentPassword { get; set; } = string.Empty;
    [BindProperty] public string NewPassword { get; set; } = string.Empty;
    [BindProperty] public string ConfirmPassword { get; set; } = string.Empty;

    public bool Changed { get; private set; }

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken cancellationToken) =>
        await LoadAsync(slug, cancellationToken) ?? Page();

    public async Task<IActionResult> OnPostAsync(string slug, CancellationToken cancellationToken)
    {
        if (await LoadAsync(slug, cancellationToken) is { } elsewhere) return elsewhere;

        var (current, next, confirm) = (CurrentPassword, NewPassword, ConfirmPassword);
        (CurrentPassword, NewPassword, ConfirmPassword) = (string.Empty, string.Empty, string.Empty);

        if (current.Length == 0 || next.Length == 0)
        {
            ErrorMessage = "Enter your current password and a new one.";
            return Page();
        }

        if (!string.Equals(next, confirm, StringComparison.Ordinal))
        {
            ErrorMessage = "The new passwords don't match.";
            return Page();
        }

        var result = await Sender.Send(new ChangePasswordCommand(TenantId, UserId, Sid, current, next), cancellationToken);
        if (result.IsFailed)
        {
            ErrorMessage = result.Errors[0].Message;
            return Page();
        }

        await HttpContext.SignOutAsync(TenantSession.Scheme);
        Changed = true;
        return Page();
    }
}
