using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardenId.ClientManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBackchannelLogoutUri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BackchannelLogoutUri",
                schema: "ClientManagement",
                table: "Clients",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BackchannelLogoutUri",
                schema: "ClientManagement",
                table: "Clients");
        }
    }
}
