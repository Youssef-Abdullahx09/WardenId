using FluentResults;

namespace WardenId.Identity.Application.Tests.Fakes;

public sealed class FakeUserRepository : IUserRepository
{
    private readonly Dictionary<Guid, User> _usersById = new();

    /// <summary>The actor id passed to the most recent Create/Update/SoftDelete/Restore call.</summary>
    public Guid? LastActorId { get; private set; }

    public int CreateCallCount { get; private set; }
    public int UpdateCallCount { get; private set; }
    public int SoftDeleteCallCount { get; private set; }
    public int RestoreCallCount { get; private set; }

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
        return Task.FromResult(user is not null && user.TenantId == tenantId && !user.IsDeleted
            ? Result.Ok(user)
            : Result.Fail<User>(UserErrors.NotFound));
    }

    public Task<Result<User>> GetByIdIncludingDeletedAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
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

    /// <summary>
    /// How many of the next saves fail with a concurrency conflict. The fake can't undo the changes a handler
    /// made to the (shared, in-memory) user before saving, so use it to test how a conflict is answered, not counts.
    /// </summary>
    public int ConflictsToSimulate { get; set; }

    public Task<Result> UpdateAsync(User user, Guid userId, CancellationToken cancellationToken = default)
    {
        LastActorId = userId;
        UpdateCallCount++;
        if (TrySimulateConflict(out var conflict)) return Task.FromResult(conflict);
        _usersById[user.Id] = user;
        return Task.FromResult(Result.Ok());
    }

    public Task<Result> SoftDeleteAsync(User user, Guid userId, CancellationToken cancellationToken = default)
    {
        LastActorId = userId;
        SoftDeleteCallCount++;
        if (TrySimulateConflict(out var conflict)) return Task.FromResult(conflict);
        _usersById[user.Id] = user;
        return Task.FromResult(Result.Ok());
    }

    public Task<Result> RestoreAsync(User user, Guid userId, CancellationToken cancellationToken = default)
    {
        LastActorId = userId;
        RestoreCallCount++;
        if (TrySimulateConflict(out var conflict)) return Task.FromResult(conflict);
        user.MarkAsRestored();
        _usersById[user.Id] = user;
        return Task.FromResult(Result.Ok());
    }

    private bool TrySimulateConflict(out Result conflict)
    {
        conflict = Result.Fail(UserErrors.ConcurrencyConflict());
        if (ConflictsToSimulate <= 0) return false;
        ConflictsToSimulate--;
        return true;
    }
}
