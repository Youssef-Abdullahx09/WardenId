using Microsoft.EntityFrameworkCore;
using WardenId.Tenancy.Application.Features.SigningKeys.GetPublicKeys;
using WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetPublicKeys;

namespace WardenId.Tenancy.Application.Tests.Features.SigningKeys.GetPublicKeys;

public class HandlerTests
{
    private static async Task<TestTenancyDbContext> SeedAsync(params Tenant[] tenants)
    {
        var db = TestTenancyDbContext.CreateNew(Guid.NewGuid().ToString());
        db.Tenants.AddRange(tenants);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return db;
    }

    [Theory]
    [InlineData(Status.Active)]
    [InlineData(Status.Suspended)]
    public async Task Handle_ReturnsKeys_WhenTenantIsServable(Status status)
    {
        var (tenant, key) = TenantBuilder.Create("acme", status);
        await using var db = await SeedAsync(tenant);

        var result = await new Handler(db).Handle(new Query("acme"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(tenant.Id, result.Value.TenantId);
        Assert.Equal("acme", result.Value.Slug);
        Assert.Equal(new[] { key.PublicKeyJwk }, result.Value.PublicKeyJwks);
    }

    [Theory]
    [InlineData(Status.Provisioning)]
    [InlineData(Status.Archived)]
    public async Task Handle_ReturnsNotFound_WhenTenantStatusIsNotServable(Status status)
    {
        var (tenant, _) = TenantBuilder.Create("acme", status);
        await using var db = await SeedAsync(tenant);

        var result = await new Handler(db).Handle(new Query("acme"), CancellationToken.None);

        AssertNotFound(result);
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_WhenTenantIsSoftDeleted()
    {
        var (tenant, _) = TenantBuilder.Create("acme", Status.Active);
        tenant.MarkAsDeleted(Guid.Empty);
        await using var db = await SeedAsync(tenant);

        var result = await new Handler(db).Handle(new Query("acme"), CancellationToken.None);

        AssertNotFound(result);
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_WhenSlugIsUnknown()
    {
        var (tenant, _) = TenantBuilder.Create("acme", Status.Active);
        await using var db = await SeedAsync(tenant);

        var result = await new Handler(db).Handle(new Query("other"), CancellationToken.None);

        AssertNotFound(result);
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_WhenSlugCaseDiffers()
    {
        var (tenant, _) = TenantBuilder.Create("acme", Status.Active);
        await using var db = await SeedAsync(tenant);

        var result = await new Handler(db).Handle(new Query("ACME"), CancellationToken.None);

        AssertNotFound(result);
    }

    [Fact]
    public async Task Handle_ExcludesSoftDeletedKeys()
    {
        var (tenant, key) = TenantBuilder.Create("acme", Status.Active);
        key.MarkAsDeleted(Guid.Empty);
        await using var db = await SeedAsync(tenant);

        var result = await new Handler(db).Handle(new Query("acme"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.PublicKeyJwks);
    }

    [Fact]
    public async Task Handle_IncludesInactiveKeys()
    {
        var (tenant, key) = TenantBuilder.Create("acme", Status.Active);
        await using var db = await SeedAsync(tenant);

        // No domain operation deactivates a key yet (rotation is a future pass) — flip it through EF.
        var stored = await db.Tenants.Include(t => t.SigningKeys).SingleAsync();
        db.Entry(stored.SigningKeys.Single()).Property(nameof(SigningKey.IsActive)).CurrentValue = false;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await new Handler(db).Handle(new Query("acme"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { key.PublicKeyJwk }, result.Value.PublicKeyJwks);
    }

    private static void AssertNotFound(Result<TenantPublicKeys> result)
    {
        Assert.True(result.IsFailed);
        Assert.Equal(TenantErrorCodes.NotFound, result.Errors[0].Metadata[TenantErrorCodes.MetadataKey]);
    }
}
