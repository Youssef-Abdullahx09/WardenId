namespace WardenId.ClientManagement.Infrastructure.Persistence;

public static class SchemaConsts
{
    public const string ClientsTable = "Clients";
    public const string ClientSecretsTable = "ClientSecrets";
    public const string ClientSigningKeysTable = "ClientSigningKeys";
    public const string ClientRedirectUrisTable = "ClientRedirectUris";
    public const string ClientPostLogoutRedirectUrisTable = "ClientPostLogoutRedirectUris";
    public const string ClientCorsOriginsTable = "ClientCorsOrigins";

    /// <summary>Shadow foreign-key column every child table uses to point at its owning <c>Clients</c> row.</summary>
    public const string ClientIdForeignKey = "ClientId";

    /// <summary>Database index names.</summary>
    public static class Indexes
    {
        public const string ClientSecretsClientId = "IX_ClientSecrets_ClientId";
        public const string ClientSigningKeysClientId = "IX_ClientSigningKeys_ClientId";
        public const string ClientSigningKeysKidUnique = "IX_ClientSigningKeys_Kid_Unique";
    }
}
