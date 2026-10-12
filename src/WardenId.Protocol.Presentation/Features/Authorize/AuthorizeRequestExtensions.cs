using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;

namespace WardenId.Protocol.Presentation.Features.Authorize;

internal static class AuthorizeRequestExtensions
{
    public const string CancelParameter = "cancel";
    private const string AntiforgeryField = "__RequestVerificationToken";

    /// <summary>
    /// The authorize request as a same-tenant GET URL, so login can resume it — including requests
    /// that arrived as POST. Our own cancel/antiforgery fields are never carried over.
    /// </summary>
    public static string ToReturnUrl(this OpenIddictRequest request, string slug)
    {
        var query = QueryString.Create(request.GetParameters()
            .Where(p => p.Key is not (CancelParameter or AntiforgeryField))
            .Select(p => KeyValuePair.Create(p.Key, (string?)p.Value.ToString())));
        return $"/tenants/{slug}/authorize{query}";
    }
}
