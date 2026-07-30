using FluentResults;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Abstractions;

public interface IUserRepository
{
    Task CreateAsync(User user, Guid userId, CancellationToken cancellationToken = default);

    Task<Result<User>> GetByIdAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    Task<Result<User>> GetByEmailAsync(Guid tenantId, Email email, CancellationToken cancellationToken = default);

    Task<Result<User>> GetByUsernameAsync(Guid tenantId, Username username, CancellationToken cancellationToken = default);

    Task<bool> ExistsByEmailAsync(Guid tenantId, Email email, CancellationToken cancellationToken = default);

    Task<bool> ExistsByUsernameAsync(Guid tenantId, Username username, CancellationToken cancellationToken = default);

    Task UpdateAsync(User user, Guid userId, CancellationToken cancellationToken = default);

    Task SoftDeleteAsync(User user, Guid userId, CancellationToken cancellationToken = default);
}
