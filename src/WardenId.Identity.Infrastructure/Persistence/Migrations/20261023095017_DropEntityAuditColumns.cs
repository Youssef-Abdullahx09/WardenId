using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardenId.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropEntityAuditColumns : Migration
    {
        private static readonly string[] UserChildTables =
        [
            SchemaConsts.PasswordCredentialsTable,
            SchemaConsts.PasswordHistoryEntriesTable,
            SchemaConsts.MfaFactorsTable,
            SchemaConsts.RecoveryCodesTable,
            SchemaConsts.UserAttributesTable,
            SchemaConsts.EmailVerificationTokensTable,
            SchemaConsts.PasswordResetTokensTable
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Before IsDeleted goes: a child row a live user dropped (left soft-deleted by code older than
            // IdentityDbContext's hard delete of dropped children) would otherwise load again as live. A
            // soft-deleted child of a soft-deleted user is kept: restoring the user brings it back.
            foreach (var child in UserChildTables)
                migrationBuilder.Sql($$"""
                    DELETE FROM "{{IdentityDbContext.Schema}}"."{{child}}" c
                    WHERE c."IsDeleted"
                      AND NOT EXISTS (
                          SELECT 1 FROM "{{IdentityDbContext.Schema}}"."{{SchemaConsts.UsersTable}}" u
                          WHERE u."Id" = c."{{SchemaConsts.UserForeignKey}}" AND u."IsDeleted");
                    """);

            migrationBuilder.DropIndex(
                name: "IX_UserAttributes_IsDeleted",
                schema: "Identity",
                table: "UserAttributes");

            migrationBuilder.DropIndex(
                name: "IX_TenantPolicies_IsDeleted",
                schema: "Identity",
                table: "TenantPolicies");

            migrationBuilder.DropIndex(
                name: "IX_RecoveryCodes_IsDeleted",
                schema: "Identity",
                table: "RecoveryCodes");

            migrationBuilder.DropIndex(
                name: "IX_PasswordResetTokens_IsDeleted",
                schema: "Identity",
                table: "PasswordResetTokens");

            migrationBuilder.DropIndex(
                name: "IX_PasswordHistoryEntries_IsDeleted",
                schema: "Identity",
                table: "PasswordHistoryEntries");

            migrationBuilder.DropIndex(
                name: "IX_PasswordCredentials_IsDeleted",
                schema: "Identity",
                table: "PasswordCredentials");

            migrationBuilder.DropIndex(
                name: "IX_MfaFactors_IsDeleted",
                schema: "Identity",
                table: "MfaFactors");

            migrationBuilder.DropIndex(
                name: "IX_EmailVerificationTokens_IsDeleted",
                schema: "Identity",
                table: "EmailVerificationTokens");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                schema: "Identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                schema: "Identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastModificationAt",
                schema: "Identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastModificationById",
                schema: "Identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Identity",
                table: "UserAttributes");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                schema: "Identity",
                table: "UserAttributes");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Identity",
                table: "UserAttributes");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                schema: "Identity",
                table: "UserAttributes");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "Identity",
                table: "UserAttributes");

            migrationBuilder.DropColumn(
                name: "LastModificationAt",
                schema: "Identity",
                table: "UserAttributes");

            migrationBuilder.DropColumn(
                name: "LastModificationById",
                schema: "Identity",
                table: "UserAttributes");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Identity",
                table: "TenantPolicies");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                schema: "Identity",
                table: "TenantPolicies");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Identity",
                table: "TenantPolicies");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                schema: "Identity",
                table: "TenantPolicies");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "Identity",
                table: "TenantPolicies");

            migrationBuilder.DropColumn(
                name: "LastModificationAt",
                schema: "Identity",
                table: "TenantPolicies");

            migrationBuilder.DropColumn(
                name: "LastModificationById",
                schema: "Identity",
                table: "TenantPolicies");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Identity",
                table: "RecoveryCodes");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                schema: "Identity",
                table: "RecoveryCodes");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Identity",
                table: "RecoveryCodes");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                schema: "Identity",
                table: "RecoveryCodes");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "Identity",
                table: "RecoveryCodes");

            migrationBuilder.DropColumn(
                name: "LastModificationAt",
                schema: "Identity",
                table: "RecoveryCodes");

            migrationBuilder.DropColumn(
                name: "LastModificationById",
                schema: "Identity",
                table: "RecoveryCodes");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Identity",
                table: "PasswordResetTokens");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                schema: "Identity",
                table: "PasswordResetTokens");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Identity",
                table: "PasswordResetTokens");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                schema: "Identity",
                table: "PasswordResetTokens");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "Identity",
                table: "PasswordResetTokens");

            migrationBuilder.DropColumn(
                name: "LastModificationAt",
                schema: "Identity",
                table: "PasswordResetTokens");

            migrationBuilder.DropColumn(
                name: "LastModificationById",
                schema: "Identity",
                table: "PasswordResetTokens");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Identity",
                table: "PasswordHistoryEntries");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                schema: "Identity",
                table: "PasswordHistoryEntries");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Identity",
                table: "PasswordHistoryEntries");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                schema: "Identity",
                table: "PasswordHistoryEntries");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "Identity",
                table: "PasswordHistoryEntries");

            migrationBuilder.DropColumn(
                name: "LastModificationAt",
                schema: "Identity",
                table: "PasswordHistoryEntries");

            migrationBuilder.DropColumn(
                name: "LastModificationById",
                schema: "Identity",
                table: "PasswordHistoryEntries");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Identity",
                table: "PasswordCredentials");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                schema: "Identity",
                table: "PasswordCredentials");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Identity",
                table: "PasswordCredentials");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                schema: "Identity",
                table: "PasswordCredentials");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "Identity",
                table: "PasswordCredentials");

            migrationBuilder.DropColumn(
                name: "LastModificationAt",
                schema: "Identity",
                table: "PasswordCredentials");

            migrationBuilder.DropColumn(
                name: "LastModificationById",
                schema: "Identity",
                table: "PasswordCredentials");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Identity",
                table: "MfaFactors");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                schema: "Identity",
                table: "MfaFactors");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Identity",
                table: "MfaFactors");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                schema: "Identity",
                table: "MfaFactors");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "Identity",
                table: "MfaFactors");

            migrationBuilder.DropColumn(
                name: "LastModificationAt",
                schema: "Identity",
                table: "MfaFactors");

            migrationBuilder.DropColumn(
                name: "LastModificationById",
                schema: "Identity",
                table: "MfaFactors");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Identity",
                table: "EmailVerificationTokens");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                schema: "Identity",
                table: "EmailVerificationTokens");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Identity",
                table: "EmailVerificationTokens");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                schema: "Identity",
                table: "EmailVerificationTokens");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "Identity",
                table: "EmailVerificationTokens");

            migrationBuilder.DropColumn(
                name: "LastModificationAt",
                schema: "Identity",
                table: "EmailVerificationTokens");

            migrationBuilder.DropColumn(
                name: "LastModificationById",
                schema: "Identity",
                table: "EmailVerificationTokens");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Re-adds the columns only: the child rows Up deleted are not restored.
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                schema: "Identity",
                table: "Users",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedById",
                schema: "Identity",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModificationAt",
                schema: "Identity",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModificationById",
                schema: "Identity",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "Identity",
                table: "UserAttributes",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                schema: "Identity",
                table: "UserAttributes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Identity",
                table: "UserAttributes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedById",
                schema: "Identity",
                table: "UserAttributes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "Identity",
                table: "UserAttributes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModificationAt",
                schema: "Identity",
                table: "UserAttributes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModificationById",
                schema: "Identity",
                table: "UserAttributes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "Identity",
                table: "TenantPolicies",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                schema: "Identity",
                table: "TenantPolicies",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Identity",
                table: "TenantPolicies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedById",
                schema: "Identity",
                table: "TenantPolicies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "Identity",
                table: "TenantPolicies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModificationAt",
                schema: "Identity",
                table: "TenantPolicies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModificationById",
                schema: "Identity",
                table: "TenantPolicies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "Identity",
                table: "RecoveryCodes",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                schema: "Identity",
                table: "RecoveryCodes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Identity",
                table: "RecoveryCodes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedById",
                schema: "Identity",
                table: "RecoveryCodes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "Identity",
                table: "RecoveryCodes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModificationAt",
                schema: "Identity",
                table: "RecoveryCodes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModificationById",
                schema: "Identity",
                table: "RecoveryCodes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "Identity",
                table: "PasswordResetTokens",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                schema: "Identity",
                table: "PasswordResetTokens",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Identity",
                table: "PasswordResetTokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedById",
                schema: "Identity",
                table: "PasswordResetTokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "Identity",
                table: "PasswordResetTokens",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModificationAt",
                schema: "Identity",
                table: "PasswordResetTokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModificationById",
                schema: "Identity",
                table: "PasswordResetTokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "Identity",
                table: "PasswordHistoryEntries",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                schema: "Identity",
                table: "PasswordHistoryEntries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Identity",
                table: "PasswordHistoryEntries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedById",
                schema: "Identity",
                table: "PasswordHistoryEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "Identity",
                table: "PasswordHistoryEntries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModificationAt",
                schema: "Identity",
                table: "PasswordHistoryEntries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModificationById",
                schema: "Identity",
                table: "PasswordHistoryEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "Identity",
                table: "PasswordCredentials",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                schema: "Identity",
                table: "PasswordCredentials",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Identity",
                table: "PasswordCredentials",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedById",
                schema: "Identity",
                table: "PasswordCredentials",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "Identity",
                table: "PasswordCredentials",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModificationAt",
                schema: "Identity",
                table: "PasswordCredentials",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModificationById",
                schema: "Identity",
                table: "PasswordCredentials",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "Identity",
                table: "MfaFactors",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                schema: "Identity",
                table: "MfaFactors",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Identity",
                table: "MfaFactors",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedById",
                schema: "Identity",
                table: "MfaFactors",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "Identity",
                table: "MfaFactors",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModificationAt",
                schema: "Identity",
                table: "MfaFactors",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModificationById",
                schema: "Identity",
                table: "MfaFactors",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "Identity",
                table: "EmailVerificationTokens",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                schema: "Identity",
                table: "EmailVerificationTokens",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Identity",
                table: "EmailVerificationTokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedById",
                schema: "Identity",
                table: "EmailVerificationTokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "Identity",
                table: "EmailVerificationTokens",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModificationAt",
                schema: "Identity",
                table: "EmailVerificationTokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModificationById",
                schema: "Identity",
                table: "EmailVerificationTokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAttributes_IsDeleted",
                schema: "Identity",
                table: "UserAttributes",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_TenantPolicies_IsDeleted",
                schema: "Identity",
                table: "TenantPolicies",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryCodes_IsDeleted",
                schema: "Identity",
                table: "RecoveryCodes",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_IsDeleted",
                schema: "Identity",
                table: "PasswordResetTokens",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordHistoryEntries_IsDeleted",
                schema: "Identity",
                table: "PasswordHistoryEntries",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordCredentials_IsDeleted",
                schema: "Identity",
                table: "PasswordCredentials",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_MfaFactors_IsDeleted",
                schema: "Identity",
                table: "MfaFactors",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerificationTokens_IsDeleted",
                schema: "Identity",
                table: "EmailVerificationTokens",
                column: "IsDeleted");
        }
    }
}
