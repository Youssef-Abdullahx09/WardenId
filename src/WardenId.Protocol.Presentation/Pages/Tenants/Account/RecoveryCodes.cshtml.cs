using MediatR;
using Microsoft.AspNetCore.Mvc;
using RegenerateRecoveryCodesCommand = WardenId.Protocol.Application.Features.Account.RegenerateRecoveryCodes.Command;

namespace WardenId.Protocol.Presentation.Pages.Tenants.Account;

/// <summary>A current code from the authenticator app, then a new set of recovery codes (the old ones stop working).</summary>
public sealed class RecoveryCodesModel(ISender sender) : AccountPageModel(sender)
{
    [BindProperty] public string Code { get; set; } = string.Empty;

    /// <summary>Shown on this response only.</summary>
    public IReadOnlyList<string>? RecoveryCodes { get; private set; }

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken cancellationToken)
    {
        if (await LoadAsync(slug, cancellationToken) is { } elsewhere) return elsewhere;
        return Account.HasMfa ? Page() : ToOverview();
    }

    public async Task<IActionResult> OnPostAsync(string slug, CancellationToken cancellationToken)
    {
        if (await LoadAsync(slug, cancellationToken) is { } elsewhere) return elsewhere;
        if (!Account.HasMfa) return ToOverview();

        var code = Code.Trim();
        Code = string.Empty;
        var result = await Sender.Send(new RegenerateRecoveryCodesCommand(TenantId, UserId, code), cancellationToken);
        if (result.IsFailed)
        {
            ErrorMessage = WrongCode;
            return Page();
        }

        RecoveryCodes = result.Value;
        return Page();
    }
}
