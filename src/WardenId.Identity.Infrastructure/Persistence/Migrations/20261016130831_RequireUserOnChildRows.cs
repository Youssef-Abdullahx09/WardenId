using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardenId.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RequireUserOnChildRows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Child rows a user dropped (revoked MFA factors, cleared recovery codes, replaced
            // credentials, trimmed password history) were left behind with a null UserId instead
            // of being deleted. Nothing reaches them any more and they hold secret material.
            migrationBuilder.Sql(
                """
                DELETE FROM "Identity"."UserAttributes" WHERE "UserId" IS NULL;
                DELETE FROM "Identity"."RecoveryCodes" WHERE "UserId" IS NULL;
                DELETE FROM "Identity"."PasswordResetTokens" WHERE "UserId" IS NULL;
                DELETE FROM "Identity"."PasswordHistoryEntries" WHERE "UserId" IS NULL;
                DELETE FROM "Identity"."PasswordCredentials" WHERE "UserId" IS NULL;
                DELETE FROM "Identity"."MfaFactors" WHERE "UserId" IS NULL;
                DELETE FROM "Identity"."EmailVerificationTokens" WHERE "UserId" IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "Identity",
                table: "UserAttributes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "Identity",
                table: "RecoveryCodes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "Identity",
                table: "PasswordResetTokens",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "Identity",
                table: "PasswordHistoryEntries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "Identity",
                table: "PasswordCredentials",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "Identity",
                table: "MfaFactors",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "Identity",
                table: "EmailVerificationTokens",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "Identity",
                table: "UserAttributes",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "Identity",
                table: "RecoveryCodes",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "Identity",
                table: "PasswordResetTokens",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "Identity",
                table: "PasswordHistoryEntries",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "Identity",
                table: "PasswordCredentials",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "Identity",
                table: "MfaFactors",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "Identity",
                table: "EmailVerificationTokens",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }
    }
}
