using RenameCommand = WardenId.Tenancy.Application.Features.Tenants.Rename.Command;
using RenameHandler = WardenId.Tenancy.Application.Features.Tenants.Rename.Handler;
using SuspendCommand = WardenId.Tenancy.Application.Features.Tenants.Suspend.Command;
using SuspendHandler = WardenId.Tenancy.Application.Features.Tenants.Suspend.Handler;
using ReactivateCommand = WardenId.Tenancy.Application.Features.Tenants.Reactivate.Command;
using ReactivateHandler = WardenId.Tenancy.Application.Features.Tenants.Reactivate.Handler;
using ArchiveCommand = WardenId.Tenancy.Application.Features.Tenants.Archive.Command;
using ArchiveHandler = WardenId.Tenancy.Application.Features.Tenants.Archive.Handler;

namespace WardenId.Tenancy.Application.Tests.Features.Tenants.Lifecycle;

/// <summary>Rename, Suspend, Reactivate and Archive share one shape: load, change, save.</summary>
public class HandlerTests
{
    private static readonly Guid Actor = Guid.NewGuid();

    private static async Task<(TestTenancyDbContext Db, FakeTenantRepository Repository, Tenant Tenant)> SeedAsync(Status status)
    {
        var db = TestTenancyDbContext.CreateNew(Guid.NewGuid().ToString());
        var (tenant, _) = TenantBuilder.Create("acme", status);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return (db, new FakeTenantRepository(db), tenant);
    }

    [Fact]
    public async Task Rename_ChangesTheDisplayName_AndKeepsTheSlug()
    {
        var (db, repository, tenant) = await SeedAsync(Status.Active);
        await using var _ = db;

        var result = await new RenameHandler(repository).Handle(new RenameCommand(tenant.Id, "  Acme Corp ", Actor), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Acme Corp", tenant.DisplayName);
        Assert.Equal("acme", tenant.Slug.Value);
        Assert.Equal(Actor, repository.LastActorId);
    }

    [Fact]
    public async Task Rename_RejectsAnEmptyName()
    {
        var (db, repository, tenant) = await SeedAsync(Status.Active);
        await using var _ = db;

        var result = await new RenameHandler(repository).Handle(new RenameCommand(tenant.Id, " ", Actor), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(0, repository.UpdateCallCount);
    }

    [Fact]
    public async Task Rename_RefusesAnArchivedTenant()
    {
        var (db, repository, tenant) = await SeedAsync(Status.Archived);
        await using var _ = db;

        var result = await new RenameHandler(repository).Handle(new RenameCommand(tenant.Id, "New", Actor), CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task Suspend_ThenReactivate()
    {
        var (db, repository, tenant) = await SeedAsync(Status.Active);
        await using var _ = db;

        Assert.True((await new SuspendHandler(repository).Handle(new SuspendCommand(tenant.Id, "unpaid", Actor), CancellationToken.None)).IsSuccess);
        Assert.Equal(Status.Suspended, tenant.Status);
        Assert.Equal("unpaid", tenant.SuspendedReason);

        Assert.True((await new ReactivateHandler(repository).Handle(new ReactivateCommand(tenant.Id, Actor), CancellationToken.None)).IsSuccess);
        Assert.Equal(Status.Active, tenant.Status);
        Assert.Null(tenant.SuspendedReason);
    }

    [Fact]
    public async Task Suspend_NeedsAReason()
    {
        var (db, repository, tenant) = await SeedAsync(Status.Active);
        await using var _ = db;

        var result = await new SuspendHandler(repository).Handle(new SuspendCommand(tenant.Id, "", Actor), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(Status.Active, tenant.Status);
    }

    [Fact]
    public async Task Reactivate_RefusesAnActiveTenant()
    {
        var (db, repository, tenant) = await SeedAsync(Status.Active);
        await using var _ = db;

        Assert.True((await new ReactivateHandler(repository).Handle(new ReactivateCommand(tenant.Id, Actor), CancellationToken.None)).IsFailed);
    }

    [Fact]
    public async Task Archive_IsTerminal()
    {
        var (db, repository, tenant) = await SeedAsync(Status.Active);
        await using var _ = db;

        Assert.True((await new ArchiveHandler(repository).Handle(new ArchiveCommand(tenant.Id, Actor), CancellationToken.None)).IsSuccess);
        Assert.Equal(Status.Archived, tenant.Status);
        Assert.True((await new ArchiveHandler(repository).Handle(new ArchiveCommand(tenant.Id, Actor), CancellationToken.None)).IsFailed);
    }

    [Fact]
    public async Task Changes_ReturnNotFound_ForUnknownTenant()
    {
        var (db, repository, _) = await SeedAsync(Status.Active);
        await using var __ = db;

        var result = await new SuspendHandler(repository).Handle(new SuspendCommand(Guid.NewGuid(), "x", Actor), CancellationToken.None);

        Assert.True(result.IsFailed);
    }
}
