using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardenId.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantPolicies",
                schema: "Identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PasswordMinLength = table.Column<int>(type: "integer", nullable: true),
                    PasswordRequireUppercase = table.Column<bool>(type: "boolean", nullable: true),
                    PasswordRequireDigit = table.Column<bool>(type: "boolean", nullable: true),
                    PasswordRequireSymbol = table.Column<bool>(type: "boolean", nullable: true),
                    PasswordBreachCheckEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    PasswordHistoryCount = table.Column<int>(type: "integer", nullable: true),
                    MaxFailedLoginAttempts = table.Column<int>(type: "integer", nullable: true),
                    LockoutDuration = table.Column<TimeSpan>(type: "interval", nullable: true),
                    MfaMode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    SelfRegistrationEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModificationById = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedById = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantPolicies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantPolicies_IsDeleted",
                schema: "Identity",
                table: "TenantPolicies",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_TenantPolicies_TenantId",
                schema: "Identity",
                table: "TenantPolicies",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantPolicies",
                schema: "Identity");
        }
    }
}
