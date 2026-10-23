using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using WardenId.ClientManagement.Infrastructure.Persistence;

namespace WardenId.Audit.Infrastructure.Tests;

/// <summary>Runs ClientManagement's DropEntityAuditColumns on a database holding secret and signing key rows from before it.</summary>
[Collection(PostgresCollection.Name)]
public sealed class ClientManagementDropEntityAuditColumnsMigrationTests(PostgresFixture postgres)
{
    private const string MigrationBefore = "20261014124002_AddBackchannelLogoutUri";

    [SkippableFact]
    public async Task DeletesSoftDeletedOrphanedAndDeletedClientsSecrets_AndKeepsALiveClientsSecret()
    {
        Skip.If(postgres.SkipReason is not null, postgres.SkipReason);
        var connectionString = await postgres.CreateDatabaseAsync();
        await using (var context = CreateContext(connectionString))
            await context.GetService<IMigrator>().MigrateAsync(MigrationBefore);

        var liveClientId = await InsertClientAsync(connectionString, "live-client", isDeleted: false);
        var deletedClientId = await InsertClientAsync(connectionString, "gone-client", isDeleted: true);
        var liveSecret = await InsertSecretAsync(connectionString, liveClientId, isDeleted: false);
        var droppedSecret = await InsertSecretAsync(connectionString, liveClientId, isDeleted: true);
        var deletedClientsSecret = await InsertSecretAsync(connectionString, deletedClientId, isDeleted: false);
        var orphanedSecret = await InsertSecretAsync(connectionString, clientId: null, isDeleted: false);
        var deletedClientsKey = await InsertSigningKeyAsync(connectionString, deletedClientId);

        await using (var context = CreateContext(connectionString))
            await context.Database.MigrateAsync();

        Assert.Equal(1, await CountAsync(connectionString, "ClientSecrets", liveSecret));
        Assert.Equal(0, await CountAsync(connectionString, "ClientSecrets", droppedSecret));
        Assert.Equal(0, await CountAsync(connectionString, "ClientSecrets", deletedClientsSecret));
        Assert.Equal(0, await CountAsync(connectionString, "ClientSecrets", orphanedSecret));
        Assert.Equal(0, await CountAsync(connectionString, "ClientSigningKeys", deletedClientsKey));
    }

    private static async Task<Guid> InsertClientAsync(string connectionString, string clientId, bool isDeleted)
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(connectionString, """
            INSERT INTO "ClientManagement"."Clients"
                ("Id", "TenantId", "ClientId", "ClientType", "DisplayName", "Enabled", "IsFirstParty", "GrantType",
                 "RequirePkce", "ClientAuthenticationMethod", "ConsentRequired", "AssignedScopeIds",
                 "CreatedAt", "CreatedById", "IsDeleted")
            VALUES ($1, $2, $3, 'Confidential', 'Client', true, false, 'ClientCredentials',
                    false, 'SharedSecret', false, '{}', now(), $1, $4);
            """, id, Guid.NewGuid(), clientId, isDeleted);
        return id;
    }

    private static async Task<Guid> InsertSecretAsync(string connectionString, Guid? clientId, bool isDeleted)
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(connectionString, """
            INSERT INTO "ClientManagement"."ClientSecrets"
                ("Id", "ClientId", "HashedValue", "Label", "CreatedAt", "CreatedById", "IsDeleted")
            VALUES ($1, $2, 'hash', 'label', now(), $1, $3);
            """, id, clientId is null ? DBNull.Value : clientId.Value, isDeleted);
        return id;
    }

    private static async Task<Guid> InsertSigningKeyAsync(string connectionString, Guid clientId)
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(connectionString, """
            INSERT INTO "ClientManagement"."ClientSigningKeys"
                ("Id", "ClientId", "Kid", "PublicKeyJwk", "Algorithm", "CreatedAt", "CreatedById", "IsDeleted")
            VALUES ($1, $2, $3, '{}', 'ES256', now(), $1, false);
            """, id, clientId, $"kid-{id:N}");
        return id;
    }

    private static ClientManagementDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<ClientManagementDbContext>()
            .UseNpgsql(connectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory", ClientManagementDbContext.Schema))
            .Options);

    private static async Task ExecuteAsync(string connectionString, string sql, params object[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var parameter in parameters) command.Parameters.Add(new NpgsqlParameter { Value = parameter });
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> CountAsync(string connectionString, string table, Guid id)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"""SELECT count(*) FROM "ClientManagement"."{table}" WHERE "Id" = $1""", connection);
        command.Parameters.Add(new NpgsqlParameter { Value = id });
        return (long)(await command.ExecuteScalarAsync())!;
    }
}
