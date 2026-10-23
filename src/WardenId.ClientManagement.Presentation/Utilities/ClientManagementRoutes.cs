using WardenId.SharedKernel.Presentation.Utilities;

namespace WardenId.ClientManagement.Presentation.Utilities;

/// <summary>Route templates for the versioned client-administration endpoints.</summary>
internal static class ClientManagementRoutes
{
    public const string Clients = ApiRoutes.VersionedPrefix + "/admin/tenants/{tenantId:guid}/clients";
    public const string Client = Clients + "/{clientId:guid}";
    public const string BackchannelLogoutUri = Client + "/backchannel-logout-uri";
    public const string CorsOrigins = Client + "/cors-origins";
    public const string PostLogoutRedirectUris = Client + "/post-logout-redirect-uris";
    public const string RedirectUris = Client + "/redirect-uris";
    public const string Secrets = Client + "/secrets";
    public const string Secret = Secrets + "/{secretId:guid}";

    /// <summary>The v1 <c>Location</c> of a created client.</summary>
    public static string ClientLocation(Guid tenantId, Guid clientId) =>
        $"{ApiRoutes.V1Prefix}/admin/tenants/{tenantId}/clients/{clientId}";
}
