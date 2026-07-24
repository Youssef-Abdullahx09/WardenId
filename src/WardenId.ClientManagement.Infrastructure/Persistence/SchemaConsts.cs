namespace WardenId.ClientManagement.Infrastructure.Persistence;

public static class SchemaConsts
{
    public const string ClientsTable = "Clients";
    public const string ClientSecretsTable = "ClientSecrets";
    public const string ClientSigningKeysTable = "ClientSigningKeys";
    public const string ClientRedirectUrisTable = "ClientRedirectUris";
    public const string ClientPostLogoutRedirectUrisTable = "ClientPostLogoutRedirectUris";
    public const string ClientCorsOriginsTable = "ClientCorsOrigins";
}
