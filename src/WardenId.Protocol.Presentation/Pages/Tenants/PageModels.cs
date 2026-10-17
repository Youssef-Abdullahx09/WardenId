using Microsoft.AspNetCore.WebUtilities;
using WardenId.Protocol.Application.Features.Authorize;

namespace WardenId.Protocol.Presentation.Pages.Tenants;

/// <summary>Posts the original authorize parameters back with cancel=true → access_denied at the RP.</summary>
public sealed record ReturnToApplication(string Slug, IReadOnlyList<KeyValuePair<string, string>> Parameters, string Label)
{
    /// <summary>Null unless <paramref name="returnUrl"/> is this tenant's authorize endpoint.</summary>
    public static ReturnToApplication? For(string? returnUrl, string slug, string label)
    {
        if (!AuthorizeReturnUrl.IsAuthorizeRequest(returnUrl, slug)) return null;

        var query = QueryHelpers.ParseQuery(new Uri("https://local" + returnUrl).Query);
        var parameters = query
            .SelectMany(p => p.Value.Select(v => KeyValuePair.Create(p.Key, v ?? string.Empty)))
            .ToArray();
        return new ReturnToApplication(slug, parameters, label);
    }
}

public sealed record SignInBlockedView(string Message, ReturnToApplication? Back);

/// <summary>An authenticator app being set up, as shown: QR code, the secret to type by hand, and the code form.</summary>
/// <param name="State">The protected account enrollment state to post back, or null during sign-in (ticket cookie).</param>
public sealed record TotpEnrollmentView(string QrCodeSvgMarkup, string GroupedSecret, string? State)
{
    public static TotpEnrollmentView For(PendingTotpEnrollment enrollment, string? state = null) =>
        new(
            QrCodeSvg.Render(enrollment.OtpAuthUri, "QR code to scan with your authenticator app"),
            string.Join(' ', enrollment.Secret.Chunk(4).Select(c => new string(c))),
            state);
}
