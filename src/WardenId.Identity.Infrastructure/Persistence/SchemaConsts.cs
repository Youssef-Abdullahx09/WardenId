namespace WardenId.Identity.Infrastructure.Persistence;

public static class SchemaConsts
{
    public const string UsersTable = "Users";
    public const string PasswordCredentialsTable = "PasswordCredentials";
    public const string PasswordHistoryEntriesTable = "PasswordHistoryEntries";
    public const string MfaFactorsTable = "MfaFactors";
    public const string RecoveryCodesTable = "RecoveryCodes";
    public const string UserAttributesTable = "UserAttributes";
    public const string EmailVerificationTokensTable = "EmailVerificationTokens";
    public const string PasswordResetTokensTable = "PasswordResetTokens";
    public const string TenantPoliciesTable = "TenantPolicies";

    // The shadow foreign key every child table of Users has.
    public const string UserForeignKey = "UserId";

    // The shadow xmin concurrency token of Users, MfaFactors and RecoveryCodes.
    public const string RowVersion = "Version";
}
