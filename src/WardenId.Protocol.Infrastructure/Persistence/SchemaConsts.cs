namespace WardenId.Protocol.Infrastructure.Persistence;

public static class SchemaConsts
{
    public const string SessionsTable = "Sessions";
    public const string SessionAuthorizationsTable = "SessionAuthorizations";
    public const string TenantTokenPoliciesTable = "TenantTokenPolicies";

    // The shadow foreign key of SessionAuthorizations to its Sessions row.
    public const string SessionForeignKey = "SessionId";
}
