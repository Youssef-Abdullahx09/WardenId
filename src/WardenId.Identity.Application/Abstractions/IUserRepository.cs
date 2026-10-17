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

    /// <summary>
    /// Saves the user. Fails with <see cref="UserErrors.ConcurrencyConflict"/>, saving nothing, when another request
    /// changed the user, or one of the factors or recovery codes this save touches, since it was loaded. After a
    /// conflict the repository forgets what it loaded: load the user again before retrying.
    /// </summary>
    Task<Result> UpdateAsync(User user, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Fails like <see cref="UpdateAsync"/> on a concurrent change.</summary>
    Task<Result> SoftDeleteAsync(User user, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Undoes <see cref="SoftDeleteAsync"/>: the user is found by the normal lookups again. Fails like <see cref="UpdateAsync"/> on a concurrent change.</summary>
    Task<Result> RestoreAsync(User user, Guid userId, CancellationToken cancellationToken = default);
}
