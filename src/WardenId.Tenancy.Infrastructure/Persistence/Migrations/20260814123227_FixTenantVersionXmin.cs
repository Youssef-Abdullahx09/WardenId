using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardenId.Tenancy.Infrastructure.Persistence
{
    /// <inheritdoc />
    public partial class FixTenantVersionXmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // "xmin" is a reserved Postgres system column name — it can't be a real column.
            // The old bytea "Version" rowversion never worked (Postgres has no server-generated
            // rowversion type), so the concurrency token now rides the implicit system "xmin"
            // column instead; the real "Version" column is just dropped, nothing replaces it.
            migrationBuilder.DropColumn(
                name: "Version",
                schema: "Tenancy",
                table: "Tenants");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                schema: "Tenancy",
                table: "Tenants",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[8]);
        }
    }
}
