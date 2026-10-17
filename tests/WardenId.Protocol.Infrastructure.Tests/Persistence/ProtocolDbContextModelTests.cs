using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using WardenId.Protocol.Domain.Sessions;
using WardenId.Protocol.Domain.TokenPolicies;
using WardenId.Protocol.Infrastructure.Persistence;
using WardenId.SharedKernel.Persistence.Audit;

namespace WardenId.Protocol.Infrastructure.Tests.Persistence;

public class ProtocolDbContextModelTests
{
    // Builds the Npgsql design-time model only; no connection is opened.
    private static IModel Model()
    {
        var options = new DbContextOptionsBuilder<ProtocolDbContext>()
            .UseNpgsql("Host=localhost;Database=model-only")
            .Options;
        using var context = new ProtocolDbContext(options);
        return context.GetService<IDesignTimeModel>().Model;
    }

    private static IEntityType SessionEntity() => Model().FindEntityType(typeof(Session))!;

    [Fact]
    public void Sessions_MapToProtocolSchema()
    {
        var session = SessionEntity();

        Assert.Equal(ProtocolDbContext.Schema, session.GetSchema());
        Assert.Equal("Sessions", session.GetTableName());
    }

    [Fact]
    public void Authorizations_AreOwnedTable_KeyedBySessionAndAuthorizationId_CascadeDeleted()
    {
        var authorization = Model().FindEntityType(typeof(SessionAuthorization))!;

        Assert.True(authorization.IsOwned());
        Assert.Equal("SessionAuthorizations", authorization.GetTableName());
        Assert.Equal(ProtocolDbContext.Schema, authorization.GetSchema());
        Assert.Equal(
            new[] { "SessionId", nameof(SessionAuthorization.AuthorizationId) },
            authorization.FindPrimaryKey()!.Properties.Select(p => p.Name));
        Assert.Equal(DeleteBehavior.Cascade, authorization.FindOwnership()!.DeleteBehavior);
    }

    [Fact]
    public void EndReason_IsStoredAsNullableString()
    {
        var property = SessionEntity().FindProperty(nameof(Session.EndReason))!;

        Assert.Equal(typeof(string), property.GetProviderClrType());
        Assert.True(property.IsNullable);
    }

    [Fact]
    public void Indexes_CoverTenant_AndLiveSessionsPerSubject()
    {
        var indexes = SessionEntity().GetIndexes()
            .Select(i => string.Join(",", i.Properties.Select(p => p.Name)))
            .ToList();

        Assert.Contains("TenantId", indexes);
        Assert.Contains("SubjectId,EndedAt", indexes);
    }

    [Fact]
    public void AuditEvents_AreMapped_ButExcludedFromProtocolMigrations()
    {
        var audit = Model().FindEntityType(typeof(AuditEventRecord))!;

        Assert.Equal("audit", audit.GetSchema());
        Assert.True(audit.IsTableExcludedFromMigrations());
    }
    [Fact]
    public void TenantTokenPolicies_MapToProtocolSchema_OneRowPerTenant_WithNullableIntervals()
    {
        var policy = Model().FindEntityType(typeof(TenantTokenPolicy))!;

        Assert.Equal(ProtocolDbContext.Schema, policy.GetSchema());
        Assert.Equal("TenantTokenPolicies", policy.GetTableName());
        var tenantIndex = Assert.Single(policy.GetIndexes(), i => i.Properties.Single().Name == nameof(TenantTokenPolicy.TenantId));
        Assert.True(tenantIndex.IsUnique);
        foreach (var name in new[]
                 {
                     nameof(TenantTokenPolicy.AccessTokenLifetime), nameof(TenantTokenPolicy.IdTokenLifetime),
                     nameof(TenantTokenPolicy.RefreshTokenLifetime), nameof(TenantTokenPolicy.RefreshTokenAbsoluteLifetime),
                     nameof(TenantTokenPolicy.AuthorizationCodeLifetime), nameof(TenantTokenPolicy.SessionCookieLifetime),
                     nameof(TenantTokenPolicy.SessionAbsoluteLifetime)
                 })
        {
            var property = policy.FindProperty(name)!;
            Assert.True(property.IsNullable);
            Assert.Equal("interval", property.GetColumnType());
        }
    }
}
