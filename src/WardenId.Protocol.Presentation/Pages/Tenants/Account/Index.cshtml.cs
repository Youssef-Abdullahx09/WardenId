using System.Globalization;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using WardenId.Protocol.Application.Features.Account.Get;

namespace WardenId.Protocol.Presentation.Pages.Tenants.Account;

/// <summary>The account overview: who is signed in, two-step verification status, and links to the actions.</summary>
public sealed class IndexModel(ISender sender) : AccountPageModel(sender)
{
    /// <summary>At or below this many recovery codes the page warns the user.</summary>
    public const int FewRecoveryCodes = 2;

    public bool FewRecoveryCodesLeft => Account.HasMfa && Account.RemainingRecoveryCodes <= FewRecoveryCodes;

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken cancellationToken) =>
        await LoadAsync(slug, cancellationToken) ?? Page();

    public static string Name(AccountFactorSummary factor) =>
        string.Equals(factor.Type, "Totp", StringComparison.OrdinalIgnoreCase) ? "Authenticator app" : factor.Type;

    public static string Format(DateTimeOffset? at) =>
        at is { } value ? value.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture) : "never";
}
