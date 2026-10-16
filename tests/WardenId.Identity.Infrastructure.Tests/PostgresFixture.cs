using DotNet.Testcontainers.Builders;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace WardenId.Identity.Infrastructure.Tests;

/// <summary>
/// A migrated Identity database on real Postgres, since soft deletes, orphaned children and owned
/// types behave differently under EF InMemory. Uses a Testcontainers Postgres by default. When
/// <see cref="ServerVariable"/> holds a connection string, a throwaway database is created on that
/// server instead and dropped afterwards (for machines whose test host can't reach Docker). With
/// neither, <see cref="SkipReason"/> says why and the tests skip.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string ServerVariable = "WARDENID_TEST_POSTGRES";

    private PostgreSqlContainer? _container;
    private string? _serverConnectionString;
    private string _databaseName = "";

    public string ConnectionString { get; private set; } = "";

    /// <summary>Set when no Postgres is reachable; tests call <c>Skip.If</c> with it.</summary>
    public string? SkipReason { get; private set; }

    public async Task InitializeAsync()
    {
        _serverConnectionString = Environment.GetEnvironmentVariable(ServerVariable);

        if (string.IsNullOrWhiteSpace(_serverConnectionString))
        {
            try
            {
                _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
                await _container.StartAsync();
            }
            catch (DockerUnavailableException)
            {
                _container = null;
                SkipReason = $"Needs Postgres: start Docker, or set {ServerVariable} to a server connection string.";
                return;
            }
            ConnectionString = _container.GetConnectionString();
        }
        else
        {
            _databaseName = $"wardenid_identity_tests_{Guid.NewGuid():N}";
            await ExecuteOnServerAsync($"CREATE DATABASE \"{_databaseName}\"");
            ConnectionString = new NpgsqlConnectionStringBuilder(_serverConnectionString) { Database = _databaseName }.ConnectionString;
        }

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
            return;
        }

        if (_databaseName.Length == 0) return;

        NpgsqlConnection.ClearAllPools();
        await ExecuteOnServerAsync($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)");
    }

    public IdentityDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(ConnectionString).Options);

    public async Task<long> CountAsync(string sql, params object[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var parameter in parameters) command.Parameters.Add(new NpgsqlParameter { Value = parameter });
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task ExecuteOnServerAsync(string sql)
    {
        var builder = new NpgsqlConnectionStringBuilder(_serverConnectionString) { Database = "postgres" };
        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
