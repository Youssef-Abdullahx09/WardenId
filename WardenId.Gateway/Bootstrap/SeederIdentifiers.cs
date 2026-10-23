namespace WardenId.Gateway.Bootstrap;

/// <summary>Logger categories and system actor ids of the startup seeders (actor ids are persisted in audit events).</summary>
public static class SeederIdentifiers
{
    public static class LoggerCategories
    {
        public const string MasterTenant = "WardenId.Gateway.MasterTenantSeed";
        public const string MasterAdmin = "WardenId.Gateway.MasterAdminSeed";
        public const string MasterClient = "WardenId.Gateway.MasterClientSeed";
    }

    public static class SystemActorIds
    {
        public const string MasterTenant = "master-tenant-seeder";
        public const string MasterAdmin = "master-admin-seeder";
        public const string MasterClient = "master-client-seeder";
    }

    /// <summary>Label of the master client's seeded secret.</summary>
    public const string ClientSecretLabel = "seeded";
}
