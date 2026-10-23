using FluentResults;
using Microsoft.EntityFrameworkCore;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Infrastructure.Persistence;

public sealed class UserRepository(
    IIdentityDbContext dbContext) : IUserRepository
{
    public async Task CreateAsync(
        User user,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Users.AddAsync(user, cancellationToken);
        await dbContext.SaveChangesAsync(userId, cancellationToken);
    }

    public async Task<Result<User>> GetByIdAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await Query()
            .Where(u => u.Id == userId && u.TenantId == tenantId && !u.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        return user is null ? Result.Fail(UserErrors.NotFound) : user;
    }

    public async Task<Result<User>> GetByIdIncludingDeletedAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await Query()
            .Where(u => u.Id == userId && u.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        return user is null ? Result.Fail(UserErrors.NotFound) : user;
    }

    public async Task<Result<User>> GetByEmailAsync(
        Guid tenantId,
        Email email,
        CancellationToken cancellationToken = default)
    {
        var user = await Query()
            .Where(u => u.Email == email && u.TenantId == tenantId && !u.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        return user is null ? Result.Fail(UserErrors.NotFound) : user;
    }

    public async Task<Result<User>> GetByUsernameAsync(
        Guid tenantId,
        Username username,
        CancellationToken cancellationToken = default)
    {
        var user = await Query()
            .Where(u => u.Username == username && u.TenantId == tenantId && !u.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        return user is null ? Result.Fail(UserErrors.NotFound) : user;
    }

    // Soft-deleted users count: the unique (TenantId, Email) and (TenantId, Username) indexes
    // include their rows, which keep the email and username for a restore.
    public Task<bool> ExistsByEmailAsync(
        Guid tenantId,
        Email email,
        CancellationToken cancellationToken = default) =>
        dbContext.Users.AnyAsync(u => u.Email == email && u.TenantId == tenantId, cancellationToken);

    public Task<bool> ExistsByUsernameAsync(
        Guid tenantId,
        Username username,
        CancellationToken cancellationToken = default) =>
        dbContext.Users.AnyAsync(u => u.Username == username && u.TenantId == tenantId, cancellationToken);

    public Task<Result> UpdateAsync(
        User user,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        SaveAsync(userId, cancellationToken);

    public Task<Result> SoftDeleteAsync(
        User user,
        Guid userId,
        CancellationToken cancellationToken = default)
        => SaveAsync(userId, cancellationToken);

    // The unique (TenantId, Email) and (TenantId, Username) indexes include soft-deleted rows,
    // so no other user can have taken this user's email or username in the meantime.
    // User.SoftDelete leaves the child rows (credential, MFA factors, ...) as they are, so they
    // come back with the user; children the user dropped earlier were deleted outright.
    public Task<Result> RestoreAsync(
        User user,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        SaveAsync(userId, cancellationToken);

    // Users, MfaFactors and RecoveryCodes carry an xmin concurrency token (UserConfiguration and siblings): an
    // UPDATE or DELETE of a row someone else changed since it was loaded matches no row and EF throws. The
    // transaction is rolled back, so nothing of this save is kept, and the tracked state no longer matches the
    // database: it is dropped, so the caller's next load reads the current rows.
    private async Task<Result> SaveAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(userId, cancellationToken);
            return Result.Ok();
        }
        catch (DbUpdateConcurrencyException)
        {
            ((DbContext)dbContext).ChangeTracker.Clear();
            return Result.Fail(UserErrors.ConcurrencyConflict());
        }
    }

    private IQueryable<User> Query() =>
        dbContext.Users
            .Include(u => u.Credential)
            .Include(u => u.MfaFactors)
            .Include(u => u.RecoveryCodes)
            .Include(u => u.Attributes)
            .Include(u => u.PasswordHistory)
            .Include(u => u.EmailVerificationTokens)
            .Include(u => u.PasswordResetTokens);
}
