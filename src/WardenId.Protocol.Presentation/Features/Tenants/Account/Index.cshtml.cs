using System.Globalization;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using WardenId.Identity.Integration.Command.Features.Users.GetAccount;
using WardenId.Protocol.Application.Features.Account.Get;

namespace WardenId.Protocol.Presentation.Features.Tenants.Account;

/// <summary>The account overview: who is signed in, two-step verification status, and links to the actions.</summary>
public sealed class IndexModel(ISender sender) : AccountPageModel(sender)
{
    /// <summary>At or below this many recovery codes the page warns the user.</summary>
    public const int FewRecoveryCodes = 2;

    private const string DateFormat = "yyyy-MM-dd HH:mm 'UTC'";

    public bool FewRecoveryCodesLeft => Account.HasMfa && Account.RemainingRecoveryCodes <= FewRecoveryCodes;

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken cancellationToken) =>
        await LoadAsync(slug, cancellationToken) ?? Page();

    public static string Name(AccountFactorSummary factor) =>
        factor.Type == AccountFactorType.Totp ? "Authenticator app" : factor.Type.ToString();

    public static string Format(DateTimeOffset? at) =>
        at is { } value ? value.UtcDateTime.ToString(DateFormat, CultureInfo.InvariantCulture) : "never";
}
