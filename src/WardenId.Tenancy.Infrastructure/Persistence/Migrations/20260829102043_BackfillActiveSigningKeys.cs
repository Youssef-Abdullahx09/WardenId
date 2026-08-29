using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardenId.Tenancy.Infrastructure.Persistence
{
    /// <inheritdoc />
    public partial class BackfillActiveSigningKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfills tenants provisioned before SigningKey.IsActive was set to true on
            // creation (Task 2 of the Protocol login pass) — otherwise GetActiveForSigning
            // would never find an active key for any already-existing tenant, including the
            // seeded master tenant. Safe because no tenant can currently have more than one
            // non-deleted signing key (Tenant.AssignInitialSigningKey rejects a second),
            // which is what the partial unique index on (TenantId, IsActive) relies on.
            migrationBuilder.Sql("UPDATE \"Tenancy\".\"SigningKeys\" SET \"IsActive\" = true WHERE \"IsDeleted\" = false;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
