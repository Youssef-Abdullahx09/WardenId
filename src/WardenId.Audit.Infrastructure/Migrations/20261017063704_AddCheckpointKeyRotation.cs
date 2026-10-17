using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardenId.Audit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckpointKeyRotation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeactivatedAt",
                schema: "audit",
                table: "checkpoint_signing_keys",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PreviousKeyId",
                schema: "audit",
                table: "checkpoint_signing_keys",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RotationAttestation",
                schema: "audit",
                table: "checkpoint_signing_keys",
                type: "bytea",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeactivatedAt",
                schema: "audit",
                table: "checkpoint_signing_keys");

            migrationBuilder.DropColumn(
                name: "PreviousKeyId",
                schema: "audit",
                table: "checkpoint_signing_keys");

            migrationBuilder.DropColumn(
                name: "RotationAttestation",
                schema: "audit",
                table: "checkpoint_signing_keys");
        }
    }
}
