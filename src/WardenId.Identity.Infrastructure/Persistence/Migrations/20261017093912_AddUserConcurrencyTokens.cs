using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardenId.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserConcurrencyTokens : Migration
    {
        // Users, MfaFactors and RecoveryCodes get an xmin concurrency token (the "Version" shadow property). "xmin"
        // is Postgres' system column, present on every row already, and can't be added as a real column, so the
        // AddColumn operations EF generated were removed (like Tenancy's FixTenantVersionXmin). This migration only
        // records the model change.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
