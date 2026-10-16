using Microsoft.EntityFrameworkCore;
using WardenId.Tenancy.Application.Features.SigningKeys.Retire;
using RotateCommand = WardenId.Tenancy.Application.Features.SigningKeys.Rotate.Command;
using RotateHandler = WardenId.Tenancy.Application.Features.SigningKeys.Rotate.Handler;

namespace WardenId.Tenancy.Application.Tests.Features.SigningKeys.Retire;

public class HandlerTests
{
    private static readonly Guid Actor = Guid.NewGuid();

    private static async Task<(TestTenancyDbContext Db, FakeTenantRepository Repository, Tenant Tenant, SigningKey Key)> SeedAsync()
    {
        var db = TestTenancyDbContext.CreateNew(Guid.NewGuid().ToString());
        var (tenant, key) = TenantBuilder.Create("acme", Status.Active);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return (db, new FakeTenantRepository(db), tenant, key);
    }

    [Fact]
    public async Task Handle_RetiresARetiringKey()
    {
        var (db, repository, tenant, previous) = await SeedAsync();
        await using var _ = db;
        Assert.True((await new RotateHandler(repository, new FakeSigningKeyGenerator())
            .Handle(new RotateCommand(tenant.Id, Actor), CancellationToken.None)).IsSuccess);
        db.ChangeTracker.Clear();

        var result = await new Handler(repository).Handle(new Command(tenant.Id, previous.SigningKeyId, Actor), CancellationToken.None);

        Assert.True(result.IsSuccess);
        db.ChangeTracker.Clear();
        var stored = await db.Tenants.Include(t => t.SigningKeys).SingleAsync();
        var retired = stored.SigningKeys.Single(k => k.SigningKeyId == previous.SigningKeyId);
        Assert.Equal(SigningKeyStatus.Retired, retired.Status);
        Assert.NotNull(retired.RetiredAt);
    }

    [Fact]
    public async Task Handle_RefusesTheActiveKey()
    {
        var (db, repository, tenant, key) = await SeedAsync();
        await using var _ = db;

        var result = await new Handler(repository).Handle(new Command(tenant.Id, key.SigningKeyId, Actor), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(TenantErrorCodes.Validation, result.Errors[0].Metadata[TenantErrorCodes.MetadataKey]);
        Assert.Equal(0, repository.UpdateCallCount);
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_ForUnknownKey()
    {
        var (db, repository, tenant, _) = await SeedAsync();
        await using var _ = db;

        var result = await new Handler(repository).Handle(new Command(tenant.Id, Guid.NewGuid(), Actor), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(TenantErrorCodes.NotFound, result.Errors[0].Metadata[TenantErrorCodes.MetadataKey]);
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_ForUnknownTenant()
    {
        var (db, repository, _, key) = await SeedAsync();
        await using var _ = db;

        var result = await new Handler(repository).Handle(new Command(Guid.NewGuid(), key.SigningKeyId, Actor), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(TenantErrorCodes.NotFound, result.Errors[0].Metadata[TenantErrorCodes.MetadataKey]);
    }
}
