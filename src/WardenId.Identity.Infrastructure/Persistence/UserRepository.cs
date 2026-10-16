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

    public Task<bool> ExistsByEmailAsync(
        Guid tenantId,
        Email email,
        CancellationToken cancellationToken = default) =>
        dbContext.Users.AnyAsync(u => u.Email == email && u.TenantId == tenantId && !u.IsDeleted, cancellationToken);

    public Task<bool> ExistsByUsernameAsync(
        Guid tenantId,
        Username username,
        CancellationToken cancellationToken = default) =>
        dbContext.Users.AnyAsync(u => u.Username == username && u.TenantId == tenantId && !u.IsDeleted, cancellationToken);

    public async Task UpdateAsync(
        User user,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(userId, cancellationToken);
    }

    public async Task SoftDeleteAsync(
        User user,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        dbContext.Users.Remove(user);
        await dbContext.SaveChangesAsync(userId, cancellationToken);
    }

    // The unique (TenantId, Email) and (TenantId, Username) indexes include soft-deleted rows,
    // so no other user can have taken this user's email or username in the meantime.
    // The child rows (credential, MFA factors, ...) were soft-deleted with the user by the
    // cascade, and are undeleted with it. Children the user dropped earlier are not among them:
    // IdentityDbContext deletes those rows outright.
    public async Task RestoreAsync(
        User user,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        user.MarkAsRestored();
        foreach (var child in ChildrenOf(user).Where(c => c.IsDeleted)) child.MarkAsRestored();
        await dbContext.SaveChangesAsync(userId, cancellationToken);
    }

    private static IEnumerable<Entity> ChildrenOf(User user) =>
        new Entity?[] { user.Credential }.OfType<Entity>()
            .Concat(user.MfaFactors)
            .Concat(user.RecoveryCodes)
            .Concat(user.Attributes)
            .Concat(user.PasswordHistory)
            .Concat(user.EmailVerificationTokens)
            .Concat(user.PasswordResetTokens);

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
