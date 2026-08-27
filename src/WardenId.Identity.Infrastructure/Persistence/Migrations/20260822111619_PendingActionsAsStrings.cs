using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardenId.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PendingActionsAsStrings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Postgres forbids subqueries in ALTER COLUMN TYPE's USING clause entirely (even
            // via ARRAY(...)), so the int[] -> text[] conversion goes through a shadow column
            // instead: add text[] column, backfill via UPDATE (subqueries ARE allowed there),
            // drop the old column, rename the new one into place.
            migrationBuilder.Sql(
                """
                ALTER TABLE "Identity"."Users" ADD COLUMN "PendingActionsNew" text[];

                UPDATE "Identity"."Users"
                SET "PendingActionsNew" = ARRAY(
                    SELECT CASE elem
                        WHEN 1 THEN 'ResetPassword'
                        WHEN 2 THEN 'AcceptTos'
                        WHEN 3 THEN 'EnrollMfa'
                        WHEN 4 THEN 'VerifyEmail'
                    END
                    FROM unnest("PendingActions") AS elem
                );

                ALTER TABLE "Identity"."Users" ALTER COLUMN "PendingActionsNew" SET NOT NULL;
                ALTER TABLE "Identity"."Users" DROP COLUMN "PendingActions";
                ALTER TABLE "Identity"."Users" RENAME COLUMN "PendingActionsNew" TO "PendingActions";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "Identity"."Users" ADD COLUMN "PendingActionsOld" integer[];

                UPDATE "Identity"."Users"
                SET "PendingActionsOld" = ARRAY(
                    SELECT CASE elem
                        WHEN 'ResetPassword' THEN 1
                        WHEN 'AcceptTos' THEN 2
                        WHEN 'EnrollMfa' THEN 3
                        WHEN 'VerifyEmail' THEN 4
                    END
                    FROM unnest("PendingActions") AS elem
                );

                ALTER TABLE "Identity"."Users" ALTER COLUMN "PendingActionsOld" SET NOT NULL;
                ALTER TABLE "Identity"."Users" DROP COLUMN "PendingActions";
                ALTER TABLE "Identity"."Users" RENAME COLUMN "PendingActionsOld" TO "PendingActions";
                """);
        }
    }
}
