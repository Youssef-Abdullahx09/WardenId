namespace WardenId.Tenancy.Infrastructure.Persistence;

public static class SchemaConsts
{
    public const string TenantsTable =  "Tenants";
    public const string SigningKeysTable =  "SigningKeys";

    /// <summary>Shadow foreign key from <c>SigningKeys</c> to its owning tenant.</summary>
    public const string SigningKeyTenantIdColumn = "TenantId";

    public const string SigningKeysTenantIdIndex = "IX_TenantSigningKeys_TenantId";
    public const string SigningKeysActiveUniqueIndex = "IX_TenantSigningKeys_TenantId_ActiveUnique";
}