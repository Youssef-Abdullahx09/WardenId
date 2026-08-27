using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardenId.Audit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSegmentsAndCheckpoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "segments",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SequenceStart = table.Column<long>(type: "bigint", nullable: false),
                    SequenceEnd = table.Column<long>(type: "bigint", nullable: false),
                    MerkleRoot = table.Column<byte[]>(type: "bytea", nullable: false),
                    PreviousHeaderHash = table.Column<byte[]>(type: "bytea", nullable: true),
                    HeaderHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    SealedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_segments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "checkpoints",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SegmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    TreeSize = table.Column<long>(type: "bigint", nullable: false),
                    RootHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    Kid = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Signature = table.Column<byte[]>(type: "bytea", nullable: false),
                    SignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_checkpoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_checkpoints_segments_SegmentId",
                        column: x => x.SegmentId,
                        principalSchema: "audit",
                        principalTable: "segments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_checkpoints_Channel_SignedAt",
                schema: "audit",
                table: "checkpoints",
                columns: new[] { "Channel", "SignedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_checkpoints_SegmentId",
                schema: "audit",
                table: "checkpoints",
                column: "SegmentId");

            migrationBuilder.CreateIndex(
                name: "IX_segments_Channel_SequenceEnd",
                schema: "audit",
                table: "segments",
                columns: new[] { "Channel", "SequenceEnd" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "checkpoints",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "segments",
                schema: "audit");
        }
    }
}
