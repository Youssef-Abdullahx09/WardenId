using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;
using WardenId.Protocol.Application;

namespace WardenId.Protocol.Presentation.Features.Authorize;

internal static class AuthorizeRequestExtensions
{
    private const string AntiforgeryField = "__RequestVerificationToken";

    /// <summary>
    /// The authorize request as a same-tenant GET URL, so login can resume it — including requests
    /// that arrived as POST. Our own cancel/antiforgery fields are never carried over.
    /// </summary>
    public static string ToReturnUrl(this OpenIddictRequest request, string slug)
    {
        var query = QueryString.Create(request.GetParameters()
            .Where(p => p.Key is not (TenantPages.FormFields.Cancel or AntiforgeryField))
            .Select(p => KeyValuePair.Create(p.Key, (string?)p.Value.ToString())));
        return $"{ProtocolPaths.Tenant(slug, ProtocolPaths.Endpoints.Authorize)}{query}";
    }
}
