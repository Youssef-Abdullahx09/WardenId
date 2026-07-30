using FluentResults;

namespace WardenId.Identity.Application.Tests.Fakes;

public sealed class FakeUserRepository : IUserRepository
{
    private readonly Dictionary<Guid, User> _usersById = new();

    public Task CreateAsync(User user, Guid userId, CancellationToken cancellationToken = default)
    {
        _usersById[user.Id] = user;
        return Task.CompletedTask;
    }

    public Task<Result<User>> GetByIdAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var user = _usersById.GetValueOrDefault(userId);
        return Task.FromResult(user is not null && user.TenantId == tenantId
            ? Result.Ok(user)
            : Result.Fail<User>(UserErrors.NotFound));
    }

    public Task<Result<User>> GetByEmailAsync(Guid tenantId, Email email, CancellationToken cancellationToken = default)
    {
        var user = _usersById.Values.FirstOrDefault(u => u.TenantId == tenantId && u.Email == email);
        return Task.FromResult(user is not null ? Result.Ok(user) : Result.Fail<User>(UserErrors.NotFound));
    }

    public Task<Result<User>> GetByUsernameAsync(Guid tenantId, Username username, CancellationToken cancellationToken = default)
    {
        var user = _usersById.Values.FirstOrDefault(u => u.TenantId == tenantId && u.Username == username);
        return Task.FromResult(user is not null ? Result.Ok(user) : Result.Fail<User>(UserErrors.NotFound));
    }

    public Task<bool> ExistsByEmailAsync(Guid tenantId, Email email, CancellationToken cancellationToken = default) =>
        Task.FromResult(_usersById.Values.Any(u => u.TenantId == tenantId && u.Email == email));

    public Task<bool> ExistsByUsernameAsync(Guid tenantId, Username username, CancellationToken cancellationToken = default) =>
        Task.FromResult(_usersById.Values.Any(u => u.TenantId == tenantId && u.Username == username));

    public Task UpdateAsync(User user, Guid userId, CancellationToken cancellationToken = default)
    {
        _usersById[user.Id] = user;
        return Task.CompletedTask;
    }

    public Task SoftDeleteAsync(User user, Guid userId, CancellationToken cancellationToken = default)
    {
        _usersById[user.Id] = user;
        return Task.CompletedTask;
    }
}
