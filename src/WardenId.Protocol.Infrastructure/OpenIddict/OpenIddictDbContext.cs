using Microsoft.EntityFrameworkCore;

namespace WardenId.Protocol.Infrastructure.OpenIddict;

/// <summary>
/// OpenIddict's authorization/token (and unused application/scope) tables, in their own schema.
/// Deliberately a plain DbContext without the shared SaveChanges interceptors: these rows are not
/// aggregates, so no CAP outbox transaction and no audit capture per issued token.
/// </summary>
public sealed class OpenIddictDbContext(DbContextOptions<OpenIddictDbContext> options) : DbContext(options)
{
    public const string Schema = "OpenIddict";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        base.OnModelCreating(modelBuilder);
    }
}
