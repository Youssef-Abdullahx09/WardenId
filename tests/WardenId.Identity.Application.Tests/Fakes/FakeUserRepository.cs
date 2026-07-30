using FluentResults;

namespace WardenId.Identity.Application.Tests.Fakes;

public sealed class FakeUserRepository : IUserRepository
{
    private readonly Dictionary<Guid, User> _usersById = new();

    /// <summary>The actor id passed to the most recent Create/Update/SoftDelete call.</summary>
    public Guid? LastActorId { get; private set; }

    public int CreateCallCount { get; private set; }
    public int UpdateCallCount { get; private set; }
    public int SoftDeleteCallCount { get; private set; }

    public Task CreateAsync(User user, Guid userId, CancellationToken cancellationToken = default)
    {
        LastActorId = userId;
        CreateCallCount++;
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
        LastActorId = userId;
        UpdateCallCount++;
        _usersById[user.Id] = user;
        return Task.CompletedTask;
    }

    public Task SoftDeleteAsync(User user, Guid userId, CancellationToken cancellationToken = default)
    {
        LastActorId = userId;
        SoftDeleteCallCount++;
        _usersById[user.Id] = user;
        return Task.CompletedTask;
    }
}
