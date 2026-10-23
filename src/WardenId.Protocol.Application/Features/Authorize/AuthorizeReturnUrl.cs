using System.Diagnostics.CodeAnalysis;

namespace WardenId.Protocol.Application.Features.Authorize;

/// <summary>
/// The login page only ever sends the user back to the same tenant's authorize endpoint or the same tenant's
/// account pages, so a crafted ReturnUrl can't turn login into an open redirect or a cross-tenant hop.
/// </summary>
public static class AuthorizeReturnUrl
{
    /// <summary>The tenant's authorize request or one of its account pages.</summary>
    public static bool IsAllowed(string? returnUrl, string slug) =>
        IsAuthorizeRequest(returnUrl, slug) || IsAccountPage(returnUrl, slug);

    /// <summary><c>/tenants/{slug}/authorize</c>, with or without a query.</summary>
    public static bool IsAuthorizeRequest(string? returnUrl, string slug)
    {
        if (!IsPlainLocalPath(returnUrl)) return false;

        var prefix = ProtocolPaths.Tenant(slug, ProtocolPaths.Endpoints.Authorize);
        if (!returnUrl.StartsWith(prefix, StringComparison.Ordinal)) return false;

        return returnUrl.Length == prefix.Length || returnUrl[prefix.Length] == '?';
    }

    /// <summary>
    /// <c>/tenants/{slug}/account</c> or a page below it: each segment only lowercase letters, digits and hyphens,
    /// so no query, no dots (no <c>..</c>), no empty segment (no <c>//</c>).
    /// </summary>
    public static bool IsAccountPage(string? returnUrl, string slug)
    {
        if (!IsPlainLocalPath(returnUrl)) return false;

        var prefix = ProtocolPaths.Tenant(slug, ProtocolPaths.Pages.Account);
        if (!returnUrl.StartsWith(prefix, StringComparison.Ordinal)) return false;
        if (returnUrl.Length == prefix.Length) return true;
        if (returnUrl[prefix.Length] != '/') return false;

        return returnUrl[(prefix.Length + 1)..].Split('/')
            .All(segment => segment.Length > 0 && segment.All(IsSegmentChar));
    }

    private static bool IsSegmentChar(char c) => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-';

    private static bool IsPlainLocalPath([NotNullWhen(true)] string? url) =>
        !string.IsNullOrEmpty(url) && !url.Contains('\\') && !url.Any(char.IsControl);
}
