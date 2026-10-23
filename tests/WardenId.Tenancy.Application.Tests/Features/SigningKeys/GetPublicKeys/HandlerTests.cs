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
    public async Task Handle_PublishesTheActiveAndRetiringKeys_ButNotRetiredOnes()
    {
        var (tenant, first) = TenantBuilder.Create("acme", Status.Active);
        var second = NewKey();
        var third = NewKey();
        Assert.True(tenant.RotateSigningKey(second).IsSuccess);
        Assert.True(tenant.RotateSigningKey(third).IsSuccess);
        Assert.True(tenant.RetireSigningKey(first.SigningKeyId).IsSuccess);
        await using var db = await SeedAsync(tenant);

        var result = await new Handler(db).Handle(new Query("acme"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            new[] { second.PublicKeyJwk, third.PublicKeyJwk }.Order(),
            result.Value.PublicKeyJwks.Order());
    }

    private static SigningKey NewKey()
    {
        var keyId = Guid.NewGuid();
        return SigningKey.CreateEs256(keyId, TenantBuilder.Jwk(keyId), [1]);
    }

    private static void AssertNotFound(Result<TenantPublicKeys> result)
    {
        Assert.True(result.IsFailed);
        Assert.Equal(TenantErrorCodes.NotFound, result.Errors[0].Metadata[TenantErrorCodes.MetadataKey]);
    }
}
