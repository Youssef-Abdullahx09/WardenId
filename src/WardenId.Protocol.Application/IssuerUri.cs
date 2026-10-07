namespace WardenId.Protocol.Application;

/// <summary>
/// Single source of truth for a tenant's issuer string. The token "iss" claim and the discovery
/// "issuer" must match byte-for-byte (OIDC Discovery §4.3), so nothing else may build it.
/// </summary>
public static class IssuerUri
{
    public static string For(string issuerBaseUri, string slug) =>
        $"{issuerBaseUri.TrimEnd('/')}/tenants/{slug}";
}
