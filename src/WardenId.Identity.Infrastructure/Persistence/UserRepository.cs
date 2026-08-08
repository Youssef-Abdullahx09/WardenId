using FluentResults;
using Microsoft.EntityFrameworkCore;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;

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
