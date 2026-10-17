using Microsoft.AspNetCore.WebUtilities;
using WardenId.Protocol.Application.Features.Authorize;

namespace WardenId.Protocol.Presentation.Pages.Tenants;

/// <summary>Posts the original authorize parameters back with cancel=true → access_denied at the RP.</summary>
public sealed record ReturnToApplication(string Slug, IReadOnlyList<KeyValuePair<string, string>> Parameters, string Label)
{
    /// <summary>Null unless <paramref name="returnUrl"/> is this tenant's authorize endpoint.</summary>
    public static ReturnToApplication? For(string? returnUrl, string slug, string label)
    {
        if (!AuthorizeReturnUrl.IsAllowed(returnUrl, slug)) return null;

        var query = QueryHelpers.ParseQuery(new Uri("https://local" + returnUrl).Query);
        var parameters = query
            .SelectMany(p => p.Value.Select(v => KeyValuePair.Create(p.Key, v ?? string.Empty)))
            .ToArray();
        return new ReturnToApplication(slug, parameters, label);
    }
}

public sealed record SignInBlockedView(string Message, ReturnToApplication? Back);
