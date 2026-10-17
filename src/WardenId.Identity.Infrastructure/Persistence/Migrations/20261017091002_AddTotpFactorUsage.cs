using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardenId.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTotpFactorUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Every factor before this migration came from the removed admin enroll endpoint, which stored an opaque
            // value rather than a Data Protection-protected TOTP secret, so none of them can check a code; recovery
            // codes only came from that same endpoint. Both go. EnrollMfa isn't added for their users: Authenticate
            // asks for enrollment whenever the tenant's policy requires MFA and the user has no enabled factor.
            // Down() can't bring these rows back.
            migrationBuilder.Sql("""DELETE FROM "Identity"."RecoveryCodes";""");
            migrationBuilder.Sql("""DELETE FROM "Identity"."MfaFactors";""");

            migrationBuilder.AddColumn<long>(
                name: "LastAcceptedTimeStep",
                schema: "Identity",
                table: "MfaFactors",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastUsedAtUtc",
                schema: "Identity",
                table: "MfaFactors",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The factors and recovery codes Up() deleted are gone for good: Down() only drops the new columns.
            migrationBuilder.DropColumn(
                name: "LastAcceptedTimeStep",
                schema: "Identity",
                table: "MfaFactors");

            migrationBuilder.DropColumn(
                name: "LastUsedAtUtc",
                schema: "Identity",
                table: "MfaFactors");
        }
    }
}
