namespace WardenId.Protocol.Application.Features.Authorize;

/// <summary>
/// The login page only ever sends the user back to the same tenant's authorize endpoint, so a
/// crafted ReturnUrl can't turn login into an open redirect or a cross-tenant hop.
/// </summary>
public static class AuthorizeReturnUrl
{
    public static bool IsAllowed(string? returnUrl, string slug)
    {
        if (string.IsNullOrEmpty(returnUrl) || returnUrl.Contains('\\') || returnUrl.Any(char.IsControl))
            return false;

        var prefix = $"/tenants/{slug}/authorize";
        if (!returnUrl.StartsWith(prefix, StringComparison.Ordinal)) return false;

        return returnUrl.Length == prefix.Length || returnUrl[prefix.Length] == '?';
    }
}
