using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardenId.Audit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckpointSigningKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "checkpoint_signing_keys",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicKeyJwk = table.Column<string>(type: "jsonb", nullable: false),
                    EncryptedPrivateKey = table.Column<byte[]>(type: "bytea", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_checkpoint_signing_keys", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CheckpointSigningKeys_ActiveUnique",
                schema: "audit",
                table: "checkpoint_signing_keys",
                column: "IsActive",
                unique: true,
                filter: "\"IsActive\" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "checkpoint_signing_keys",
                schema: "audit");
        }
    }
}
