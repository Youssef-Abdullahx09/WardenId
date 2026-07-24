using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardenId.ClientManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClientManagementSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ClientManagement");

            migrationBuilder.CreateTable(
                name: "Clients",
                schema: "ClientManagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ClientType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsFirstParty = table.Column<bool>(type: "boolean", nullable: false),
                    GrantType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RequirePkce = table.Column<bool>(type: "boolean", nullable: false),
                    AccessTokenSeconds = table.Column<int>(type: "integer", nullable: true),
                    IdTokenSeconds = table.Column<int>(type: "integer", nullable: true),
                    RefreshTokenSeconds = table.Column<int>(type: "integer", nullable: true),
                    ClientAuthenticationMethod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ConsentRequired = table.Column<bool>(type: "boolean", nullable: false),
                    AssignedScopeIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
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
                    table.PrimaryKey("PK_Clients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClientCorsOrigins",
                schema: "ClientManagement",
                columns: table => new
                {
                    Value = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientCorsOrigins", x => new { x.ClientId, x.Value });
                    table.ForeignKey(
                        name: "FK_ClientCorsOrigins_Clients_ClientId",
                        column: x => x.ClientId,
                        principalSchema: "ClientManagement",
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClientPostLogoutRedirectUris",
                schema: "ClientManagement",
                columns: table => new
                {
                    Value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientPostLogoutRedirectUris", x => new { x.ClientId, x.Value });
                    table.ForeignKey(
                        name: "FK_ClientPostLogoutRedirectUris_Clients_ClientId",
                        column: x => x.ClientId,
                        principalSchema: "ClientManagement",
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClientRedirectUris",
                schema: "ClientManagement",
                columns: table => new
                {
                    Value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientRedirectUris", x => new { x.ClientId, x.Value });
                    table.ForeignKey(
                        name: "FK_ClientRedirectUris_Clients_ClientId",
                        column: x => x.ClientId,
                        principalSchema: "ClientManagement",
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClientSecrets",
                schema: "ClientManagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HashedValue = table.Column<string>(type: "text", nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_ClientSecrets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientSecrets_Clients_ClientId",
                        column: x => x.ClientId,
                        principalSchema: "ClientManagement",
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClientSigningKeys",
                schema: "ClientManagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kid = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PublicKeyJwk = table.Column<string>(type: "jsonb", nullable: false),
                    Algorithm = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_ClientSigningKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientSigningKeys_Clients_ClientId",
                        column: x => x.ClientId,
                        principalSchema: "ClientManagement",
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clients_IsDeleted",
                schema: "ClientManagement",
                table: "Clients",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_TenantId",
                schema: "ClientManagement",
                table: "Clients",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_TenantId_ClientId",
                schema: "ClientManagement",
                table: "Clients",
                columns: new[] { "TenantId", "ClientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientSecrets_ClientId",
                schema: "ClientManagement",
                table: "ClientSecrets",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientSecrets_IsDeleted",
                schema: "ClientManagement",
                table: "ClientSecrets",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ClientSigningKeys_ClientId",
                schema: "ClientManagement",
                table: "ClientSigningKeys",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientSigningKeys_IsDeleted",
                schema: "ClientManagement",
                table: "ClientSigningKeys",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ClientSigningKeys_Kid_Unique",
                schema: "ClientManagement",
                table: "ClientSigningKeys",
                column: "Kid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientCorsOrigins",
                schema: "ClientManagement");

            migrationBuilder.DropTable(
                name: "ClientPostLogoutRedirectUris",
                schema: "ClientManagement");

            migrationBuilder.DropTable(
                name: "ClientRedirectUris",
                schema: "ClientManagement");

            migrationBuilder.DropTable(
                name: "ClientSecrets",
                schema: "ClientManagement");

            migrationBuilder.DropTable(
                name: "ClientSigningKeys",
                schema: "ClientManagement");

            migrationBuilder.DropTable(
                name: "Clients",
                schema: "ClientManagement");
        }
    }
}
