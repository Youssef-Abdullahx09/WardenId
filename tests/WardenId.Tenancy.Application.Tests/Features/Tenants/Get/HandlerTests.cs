using WardenId.Tenancy.Application.Features.Tenants.Get;

namespace WardenId.Tenancy.Application.Tests.Features.Tenants.Get;

public class HandlerTests
{
    [Fact]
    public async Task Handle_ReturnsTenantWithKeyMetadata()
    {
        await using var db = TestTenancyDbContext.CreateNew(Guid.NewGuid().ToString());
        var (tenant, key) = TenantBuilder.Create("acme", Status.Active);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await new Handler(new FakeTenantRepository(db)).Handle(new Query(tenant.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("acme", result.Value.Slug);
        Assert.Equal(Status.Active, result.Value.Status);
        var summary = Assert.Single(result.Value.SigningKeys);
        Assert.Equal(key.SigningKeyId, summary.KeyId);
        Assert.True(summary.IsActive);
        Assert.Equal(SigningKeyStatus.Active, summary.Status);
        Assert.Equal(key.ActivatedAt, summary.ActivatedAt);
        Assert.Null(summary.RetiringSince);
        Assert.Null(summary.RetiredAt);
    }

    [Fact]
    public async Task Handle_ReturnsEveryKeyWithItsStatus_AfterRotation()
    {
        await using var db = TestTenancyDbContext.CreateNew(Guid.NewGuid().ToString());
        var (tenant, previous) = TenantBuilder.Create("acme", Status.Active);
        var keyId = Guid.NewGuid();
        Assert.True(tenant.RotateSigningKey(SigningKey.CreateEs256(keyId, TenantBuilder.Jwk(keyId), [9])).IsSuccess);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await new Handler(new FakeTenantRepository(db)).Handle(new Query(tenant.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.SigningKeys.Count);
        var active = result.Value.SigningKeys.Single(k => k.KeyId == keyId);
        Assert.Equal(SigningKeyStatus.Active, active.Status);
        var retiring = result.Value.SigningKeys.Single(k => k.KeyId == previous.SigningKeyId);
        Assert.Equal(SigningKeyStatus.Retiring, retiring.Status);
        Assert.False(retiring.IsActive);
        Assert.NotNull(retiring.RetiringSince);
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_ForUnknownTenant()
    {
        await using var db = TestTenancyDbContext.CreateNew(Guid.NewGuid().ToString());

        var result = await new Handler(new FakeTenantRepository(db)).Handle(new Query(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(TenantErrorCodes.NotFound, result.Errors[0].Metadata[TenantErrorCodes.MetadataKey]);
    }
}
