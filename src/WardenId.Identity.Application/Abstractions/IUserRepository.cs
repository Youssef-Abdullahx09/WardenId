using FluentResults;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Abstractions;

public interface IUserRepository
{
    Task CreateAsync(User user, Guid userId, CancellationToken cancellationToken = default);

    Task<Result<User>> GetByIdAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Like <see cref="GetByIdAsync"/>, but also finds a soft-deleted user (to restore it).</summary>
    Task<Result<User>> GetByIdIncludingDeletedAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    Task<Result<User>> GetByEmailAsync(Guid tenantId, Email email, CancellationToken cancellationToken = default);

    Task<Result<User>> GetByUsernameAsync(Guid tenantId, Username username, CancellationToken cancellationToken = default);

    /// <summary>Whether any user in the tenant, soft-deleted ones included, has this email.</summary>
    Task<bool> ExistsByEmailAsync(Guid tenantId, Email email, CancellationToken cancellationToken = default);

    /// <summary>Whether any user in the tenant, soft-deleted ones included, has this username.</summary>
    Task<bool> ExistsByUsernameAsync(Guid tenantId, Username username, CancellationToken cancellationToken = default);

    Task UpdateAsync(User user, Guid userId, CancellationToken cancellationToken = default);

    Task SoftDeleteAsync(User user, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Undoes <see cref="SoftDeleteAsync"/>: the user is found by the normal lookups again.</summary>
    Task RestoreAsync(User user, Guid userId, CancellationToken cancellationToken = default);
}
