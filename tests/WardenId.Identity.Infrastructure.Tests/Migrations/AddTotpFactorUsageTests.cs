using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace WardenId.Identity.Infrastructure.Tests.Migrations;

/// <summary>Runs AddTotpFactorUsage on a scratch database holding rows from before it.</summary>
[Collection(PostgresCollection.Name)]
public sealed class AddTotpFactorUsageTests(PostgresFixture database)
{
    private const string MigrationBefore = "20261017082130_AddTenantPolicies";

    [SkippableFact]
    public async Task DeletesTheFactorsAndRecoveryCodesOfTheOldAdminEnrollment_AndKeepsTheUsers()
    {
        Skip.If(database.SkipReason is not null, database.SkipReason);
        var connectionString = await database.CreateScratchDatabaseAsync();
        try
        {
            await using (var context = CreateContext(connectionString))
                await context.GetService<IMigrator>().MigrateAsync(MigrationBefore);

            var userId = Guid.NewGuid();
            await ExecuteAsync(connectionString, """
                INSERT INTO "Identity"."Users"
                    ("Id", "TenantId", "Email", "Username", "Status", "PendingActions", "FailedAttemptCount",
                     "CreatedAt", "CreatedById", "IsDeleted")
                VALUES ($1, $2, 'old@example.test', 'old', 'Active', '{}', 0, now(), $1, false);
                """, userId, Guid.NewGuid());
            await ExecuteAsync(connectionString, """
                INSERT INTO "Identity"."MfaFactors"
                    ("Id", "UserId", "FactorType", "CredentialData", "Enabled", "EnrolledAtUtc", "VerifiedAtUtc",
                     "CreatedAt", "CreatedById", "IsDeleted")
                VALUES ($1, $2, 'Totp', 'opaque', true, now(), now(), now(), $2, false);
                """, Guid.NewGuid(), userId);
            await ExecuteAsync(connectionString, """
                INSERT INTO "Identity"."RecoveryCodes" ("Id", "UserId", "CodeHash", "CreatedAt", "CreatedById", "IsDeleted")
                VALUES ($1, $2, 'hash', now(), $2, false);
                """, Guid.NewGuid(), userId);

            await using (var context = CreateContext(connectionString))
                await context.Database.MigrateAsync();

            Assert.Equal(0, await CountAsync(connectionString, """SELECT count(*) FROM "Identity"."MfaFactors" """));
            Assert.Equal(0, await CountAsync(connectionString, """SELECT count(*) FROM "Identity"."RecoveryCodes" """));
            Assert.Equal(1, await CountAsync(connectionString, """SELECT count(*) FROM "Identity"."Users" WHERE NOT ('EnrollMfa' = ANY("PendingActions"))"""));
        }
        finally
        {
            await database.DropScratchDatabaseAsync(connectionString);
        }
    }

    private static IdentityDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(connectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory", IdentityDbContext.Schema))
            .Options);

    private static async Task ExecuteAsync(string connectionString, string sql, params object[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var parameter in parameters) command.Parameters.Add(new NpgsqlParameter { Value = parameter });
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> CountAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }
}
