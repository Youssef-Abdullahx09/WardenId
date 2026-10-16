using DotNet.Testcontainers.Builders;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace WardenId.Audit.Infrastructure.Tests;

/// <summary>
/// Real Postgres for what EF InMemory can't run: migrations (raw SQL, partitions, the append-only
/// trigger), the xmin watermark and advisory locks. Uses a Testcontainers Postgres by default. When
/// <see cref="ServerVariable"/> holds a connection string, that server is used instead. Either way
/// each test gets its own throwaway database from <see cref="CreateDatabaseAsync"/>, dropped when the
/// fixture is disposed. With neither, <see cref="SkipReason"/> says why and the tests skip.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string ServerVariable = "WARDENID_TEST_POSTGRES";

    private PostgreSqlContainer? _container;
    private string _serverConnectionString = "";
    private readonly List<string> _databases = [];

    /// <summary>Set when no Postgres is reachable; tests call <c>Skip.If</c> with it.</summary>
    public string? SkipReason { get; private set; }

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable(ServerVariable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            _serverConnectionString = configured;
            return;
        }

        try
        {
            _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
            await _container.StartAsync();
            _serverConnectionString = _container.GetConnectionString();
        }
        catch (DockerUnavailableException)
        {
            _container = null;
            SkipReason = $"Needs Postgres: start Docker, or set {ServerVariable} to a server connection string.";
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
            return;
        }

        NpgsqlConnection.ClearAllPools();
        foreach (var database in _databases)
            await ExecuteOnServerAsync($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)");
    }

    /// <summary>A new, empty database; returns its connection string.</summary>
    public async Task<string> CreateDatabaseAsync()
    {
        var name = $"wardenid_audit_tests_{Guid.NewGuid():N}";
        await ExecuteOnServerAsync($"CREATE DATABASE \"{name}\"");
        _databases.Add(name);
        return new NpgsqlConnectionStringBuilder(_serverConnectionString) { Database = name }.ConnectionString;
    }

    public static AuditDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", AuditDbContext.Schema))
            .Options);

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

/// <summary>An <see cref="IDbContextFactory{TContext}"/> over one Postgres database.</summary>
public sealed class NpgsqlAuditDbContextFactory(string connectionString) : IDbContextFactory<AuditDbContext>
{
    public AuditDbContext CreateDbContext() => PostgresFixture.CreateContext(connectionString);
}
