using WardenId.SharedKernel.Presentation.Utilities;

namespace WardenId.Tenancy.Presentation.Utilities;

/// <summary>Route templates of the Tenancy REST endpoints. Changing one is a breaking change for callers.</summary>
internal static class TenancyRoutes
{
    public static class Tenants
    {
        public const string Collection = ApiRoutes.VersionedPrefix + "/tenants";
        public const string Item = Collection + "/{tenantId:guid}";
        public const string BySlug = Collection + "/by-slug/{slug}";
        public const string Suspend = Item + "/suspend";
        public const string Reactivate = Item + "/reactivate";
        public const string Archive = Item + "/archive";

        /// <summary>The <c>Location</c> of a created tenant (API version 1).</summary>
        public static string Location(Guid tenantId) => $"{ApiRoutes.V1Prefix}/tenants/{tenantId}";
    }

    public static class SigningKeys
    {
        public const string Rotate = Tenants.Item + "/signing-keys/rotate";
        public const string Retire = Tenants.Item + "/signing-keys/{keyId:guid}/retire";
    }
}
