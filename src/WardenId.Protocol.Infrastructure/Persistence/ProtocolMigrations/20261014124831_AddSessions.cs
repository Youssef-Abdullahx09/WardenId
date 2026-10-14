using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardenId.Protocol.Infrastructure.Persistence.ProtocolMigrations
{
    /// <inheritdoc />
    public partial class AddSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Protocol");

            migrationBuilder.CreateTable(
                name: "Sessions",
                schema: "Protocol",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndReason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
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
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SessionAuthorizations",
                schema: "Protocol",
                columns: table => new
                {
                    AuthorizationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OfflineAccess = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionAuthorizations", x => new { x.SessionId, x.AuthorizationId });
                    table.ForeignKey(
                        name: "FK_SessionAuthorizations_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "Protocol",
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_IsDeleted",
                schema: "Protocol",
                table: "Sessions",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_SubjectId_EndedAt",
                schema: "Protocol",
                table: "Sessions",
                columns: new[] { "SubjectId", "EndedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_TenantId",
                schema: "Protocol",
                table: "Sessions",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SessionAuthorizations",
                schema: "Protocol");

            migrationBuilder.DropTable(
                name: "Sessions",
                schema: "Protocol");
        }
    }
}
