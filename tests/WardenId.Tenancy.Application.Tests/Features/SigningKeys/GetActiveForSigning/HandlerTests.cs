using WardenId.Tenancy.Application.Features.SigningKeys.GetActiveForSigning;
using WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetActiveForSigning;

namespace WardenId.Tenancy.Application.Tests.Features.SigningKeys.GetActiveForSigning;

public class HandlerTests
{
    [Fact]
    public async Task Handle_ReturnsSlugWithActiveKey()
    {
        var (tenant, key) = TenantBuilder.Create("acme", Status.Active);
        await using var db = TestTenancyDbContext.CreateNew(Guid.NewGuid().ToString());
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await new Handler(db).Handle(new Query(tenant.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(key.SigningKeyId, result.Value.KeyId);
        Assert.Equal("acme", result.Value.Slug);
    }

    [Fact]
    public async Task Handle_ReturnsTheRotatedKey_NotTheRetiringOne()
    {
        var (tenant, _) = TenantBuilder.Create("acme", Status.Active);
        var keyId = Guid.NewGuid();
        var next = SigningKey.CreateEs256(keyId, TenantBuilder.Jwk(keyId), [9]);
        Assert.True(tenant.RotateSigningKey(next).IsSuccess);
        await using var db = TestTenancyDbContext.CreateNew(Guid.NewGuid().ToString());
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await new Handler(db).Handle(new Query(tenant.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(keyId, result.Value.KeyId);
    }
}
