using WardenId.Protocol.Application;
using WardenId.SharedKernel.Domain;
using WardenId.SharedKernel.Presentation;

namespace WardenId.Protocol.Presentation;

internal static class ProtocolRoutes
{
    // Issuer path root — unversioned on purpose: an issuer change breaks every relying party.
    // The slug constraint is TenantSlugFormat's pattern (SharedKernel, also used by Tenancy's Slug) with its
    // quantifier braces doubled, the route-template escape ("{1,61}" -> "{{1,61}}"). Route regex
    // constraints are case-insensitive — mixed-case slugs pass routing and 404 at lookup.
    public static readonly string TenantIssuer =
        $"/{ProtocolPaths.TenantsSegment}/{{slug:regex({EscapeRouteBraces(TenantSlugFormat.RegexPattern)})}}";

    /// <summary>An OIDC endpoint or page under the tenant issuer, e.g. <see cref="ProtocolPaths.Endpoints.Token"/>.</summary>
    public static string UnderTenantIssuer(string path) => $"{TenantIssuer}/{path}";

    private static string EscapeRouteBraces(string pattern) =>
        pattern.Replace("{", "{{", StringComparison.Ordinal).Replace("}", "}}", StringComparison.Ordinal);

    /// <summary>Versioned admin API routes, all scoped to one tenant by id.</summary>
    public static class Admin
    {
        private const string Tenant = ApiRoutes.VersionedPrefix + "/admin/tenants/{tenantId:guid}";
        private const string User = Tenant + "/users/{userId:guid}";

        public const string Sessions = Tenant + "/sessions";
        public const string Session = Sessions + "/{sessionId:guid}";
        public const string UserSessions = User + "/sessions";
        public const string UserGrants = User + "/grants";
        public const string UserGrant = UserGrants + "/{authorizationId}";
        public const string ClientGrants = Tenant + "/clients/{clientId:guid}/grants";
        public const string TokenPolicy = Tenant + "/policies/tokens";
    }
}
