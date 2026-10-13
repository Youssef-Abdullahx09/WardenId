using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardenId.Protocol.Infrastructure.Persistence.OpenIddictMigrations
{
    /// <summary>
    /// The retired ProtocolDbContext owned schema "Protocol" (RefreshTokens + its own migrations history).
    /// Its code is gone; this removes the orphan schema from existing databases. No data is kept.
    /// </summary>
    public partial class DropLegacyProtocolSchema : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP SCHEMA IF EXISTS \"Protocol\" CASCADE;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Irreversible by design: the retired context no longer exists to recreate.
        }
    }
}
