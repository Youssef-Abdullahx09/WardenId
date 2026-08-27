using Microsoft.EntityFrameworkCore;
using WardenId.Audit.Infrastructure.Persistence;

namespace WardenId.Audit.Infrastructure.Retention;

/// <summary>
/// A distinct factory type (rather than reusing IDbContextFactory&lt;AuditDbContext&gt;
/// directly) so retention can be pointed at its own, narrowly-scoped connection string
/// (docs/audit/OPEN-DECISIONS.md #8 / docs/audit/db-roles.sql) independent of every other
/// Audit consumer's connection. Not pooled — retention runs on an hourly-scale poll, not a
/// hot path, so a fresh context per call is simplest.
/// </summary>
public interface IAuditRetentionDbContextFactory : IDbContextFactory<AuditDbContext>;

internal sealed class AuditRetentionDbContextFactory(string connectionString) : IAuditRetentionDbContextFactory
{
    public AuditDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", AuditDbContext.Schema))
            .Options);

    public Task<AuditDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CreateDbContext());
}
