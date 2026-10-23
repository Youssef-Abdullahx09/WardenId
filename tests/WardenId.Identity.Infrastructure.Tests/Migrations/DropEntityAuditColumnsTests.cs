using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace WardenId.Identity.Infrastructure.Tests.Migrations;

/// <summary>Runs DropEntityAuditColumns on a scratch database holding soft-deleted child rows from before it.</summary>
[Collection(PostgresCollection.Name)]
public sealed class DropEntityAuditColumnsTests(PostgresFixture database)
{
    private const string MigrationBefore = "20261017190014_AddTermsAcceptance";

    [SkippableFact]
    public async Task DeletesTheSoftDeletedChildrenOfALiveUser_AndKeepsTheChildrenOfADeletedUser()
    {
        Skip.If(database.SkipReason is not null, database.SkipReason);
        var connectionString = await database.CreateScratchDatabaseAsync();
        try
        {
            await using (var context = CreateContext(connectionString))
                await context.GetService<IMigrator>().MigrateAsync(MigrationBefore);

            var tenantId = Guid.NewGuid();
            var liveUserId = Guid.NewGuid();
            var deletedUserId = Guid.NewGuid();
            await InsertUserAsync(connectionString, liveUserId, tenantId, "live", isDeleted: false);
            await InsertUserAsync(connectionString, deletedUserId, tenantId, "gone", isDeleted: true);
            var droppedFactorId = await InsertFactorAsync(connectionString, liveUserId, isDeleted: true);
            var liveFactorId = await InsertFactorAsync(connectionString, liveUserId, isDeleted: false);
            var deletedUsersFactorId = await InsertFactorAsync(connectionString, deletedUserId, isDeleted: true);

            await using (var context = CreateContext(connectionString))
                await context.Database.MigrateAsync();

            Assert.Equal(0, await CountFactorAsync(connectionString, droppedFactorId));
            Assert.Equal(1, await CountFactorAsync(connectionString, liveFactorId));
            Assert.Equal(1, await CountFactorAsync(connectionString, deletedUsersFactorId));
        }
        finally
        {
            await database.DropScratchDatabaseAsync(connectionString);
        }
    }

    private static Task InsertUserAsync(string connectionString, Guid userId, Guid tenantId, string name, bool isDeleted) =>
        ExecuteAsync(connectionString, """
            INSERT INTO "Identity"."Users"
                ("Id", "TenantId", "Email", "Username", "Status", "PendingActions", "FailedAttemptCount",
                 "CreatedAt", "CreatedById", "IsDeleted")
            VALUES ($1, $2, $3, $4, 'Active', '{}', 0, now(), $1, $5);
            """, userId, tenantId, $"{name}@example.test", name, isDeleted);

    private static async Task<Guid> InsertFactorAsync(string connectionString, Guid userId, bool isDeleted)
    {
        var factorId = Guid.NewGuid();
        await ExecuteAsync(connectionString, """
            INSERT INTO "Identity"."MfaFactors"
                ("Id", "UserId", "FactorType", "CredentialData", "Enabled", "EnrolledAtUtc", "VerifiedAtUtc",
                 "CreatedAt", "CreatedById", "IsDeleted")
            VALUES ($1, $2, 'Totp', 'opaque', true, now(), now(), now(), $2, $3);
            """, factorId, userId, isDeleted);
        return factorId;
    }

    private static Task<long> CountFactorAsync(string connectionString, Guid factorId) =>
        CountAsync(connectionString, """SELECT count(*) FROM "Identity"."MfaFactors" WHERE "Id" = $1""", factorId);

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

    private static async Task<long> CountAsync(string connectionString, string sql, params object[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var parameter in parameters) command.Parameters.Add(new NpgsqlParameter { Value = parameter });
        return (long)(await command.ExecuteScalarAsync())!;
    }
}
