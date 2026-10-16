using Microsoft.EntityFrameworkCore;
using WardenId.Tenancy.Application.Features.SigningKeys.Rotate;

namespace WardenId.Tenancy.Application.Tests.Features.SigningKeys.Rotate;

public class HandlerTests
{
    private static readonly Guid Actor = Guid.NewGuid();

    private static async Task<(TestTenancyDbContext Db, FakeTenantRepository Repository, Tenant Tenant, SigningKey Key)> SeedAsync(Status status)
    {
        var db = TestTenancyDbContext.CreateNew(Guid.NewGuid().ToString());
        var (tenant, key) = TenantBuilder.Create("acme", status);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return (db, new FakeTenantRepository(db), tenant, key);
    }

    [Fact]
    public async Task Handle_GeneratesAndActivatesANewKey_AndRetiresThePreviousOne()
    {
        var (db, repository, tenant, previous) = await SeedAsync(Status.Active);
        await using var _ = db;
        var generator = new FakeSigningKeyGenerator();

        var result = await new Handler(repository, generator).Handle(new Command(tenant.Id, Actor), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(generator.GeneratedKeyIds.Single(), result.Value);
        Assert.Equal(Actor, repository.LastActorId);

        db.ChangeTracker.Clear();
        var stored = await db.Tenants.Include(t => t.SigningKeys).SingleAsync();
        Assert.Equal(2, stored.SigningKeys.Count);
        Assert.Equal(SigningKeyStatus.Active, stored.SigningKeys.Single(k => k.SigningKeyId == result.Value).Status);
        Assert.Equal(SigningKeyStatus.Retiring, stored.SigningKeys.Single(k => k.SigningKeyId == previous.SigningKeyId).Status);
    }

    [Fact]
    public async Task Handle_RefusesAnArchivedTenant()
    {
        var (db, repository, tenant, _) = await SeedAsync(Status.Archived);
        await using var _ = db;

        var result = await new Handler(repository, new FakeSigningKeyGenerator()).Handle(new Command(tenant.Id, Actor), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(TenantErrorCodes.Validation, result.Errors[0].Metadata[TenantErrorCodes.MetadataKey]);
        Assert.Equal(0, repository.UpdateCallCount);
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_ForUnknownTenant()
    {
        var (db, repository, _, _) = await SeedAsync(Status.Active);
        await using var _ = db;

        var result = await new Handler(repository, new FakeSigningKeyGenerator()).Handle(new Command(Guid.NewGuid(), Actor), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(TenantErrorCodes.NotFound, result.Errors[0].Metadata[TenantErrorCodes.MetadataKey]);
    }
}
