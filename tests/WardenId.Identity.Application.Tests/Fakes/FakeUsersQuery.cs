using WardenId.Identity.Query.Contracts;
using WardenId.SharedKernel.Query.Contracts;
using Get = WardenId.Identity.Query.Contracts.Features.Users.Get;
using GetAccount = WardenId.Identity.Query.Contracts.Features.Users.GetAccount;
using GetForSignIn = WardenId.Identity.Query.Contracts.Features.Users.GetForSignIn;
using List = WardenId.Identity.Query.Contracts.Features.Users.List;

namespace WardenId.Identity.Application.Tests.Fakes;

/// <summary>
/// Projects stored domain users into the query layer's raw rows the way the SQL does (tenant match, soft-deleted
/// users left out, enums by name, UTC DateTimes, unused recovery codes counted). The List page is canned: its
/// filtering and ordering is SQL, covered on Postgres by WardenId.Identity.Infrastructure.Tests.
/// </summary>
public sealed class FakeUsersQuery : IUsersQuery
{
    private readonly List<User> _users = [];

    public Result<PaginationListResponse<List.Item>> ListResponse { get; set; } =
        Result.Ok(new PaginationListResponse<List.Item>(0, []));

    public List<List.Query> ListCalls { get; } = [];

    /// <summary>Set to make every call fail, as a missing SQL file would.</summary>
    public IError? Failure { get; set; }

    public FakeUsersQuery With(User user)
    {
        _users.Add(user);
        return this;
    }

    public Task<Result<Get.Item?>> Get(Get.Query query, CancellationToken cancellationToken)
    {
        if (Failure is not null) return Task.FromResult(Result.Fail<Get.Item?>(Failure));

        var item = Find(query.TenantId, query.UserId) is { } user
            ? new Get.Item
            {
                Id = user.Id,
                Email = user.Email.Value,
                Username = user.Username.Value,
                Status = user.Status.ToString(),
                FailedAttemptCount = user.Lockout.FailedAttemptCount,
                LockedUntilUtc = user.Lockout.LockedUntilUtc?.UtcDateTime,
                PendingActions = user.PendingActions.Select(a => a.ToString()).ToArray(),
                TermsAcceptedAtUtc = user.TermsAcceptedAtUtc?.UtcDateTime,
                CreatedAt = user.CreatedAt.UtcDateTime,
                RemainingRecoveryCodeCount = RemainingRecoveryCodes(user),
                Attributes = user.Attributes
                    .Select(a => new Get.UserAttribute { Key = a.Key, Value = a.Value, ValueType = a.ValueType.ToString() })
                    .ToArray(),
                MfaFactors = user.MfaFactors
                    .OrderBy(f => f.EnrolledAtUtc)
                    .Select(f => new Get.MfaFactor
                    {
                        Id = f.Id,
                        FactorType = f.FactorType.ToString(),
                        Enabled = f.Enabled,
                        EnrolledAtUtc = f.EnrolledAtUtc.UtcDateTime,
                        VerifiedAtUtc = f.VerifiedAtUtc?.UtcDateTime,
                        LastUsedAtUtc = f.LastUsedAtUtc?.UtcDateTime
                    })
                    .ToArray()
            }
            : null;

        return Task.FromResult(Result.Ok(item));
    }

    public Task<Result<PaginationListResponse<List.Item>>> List(List.Query query, CancellationToken cancellationToken)
    {
        ListCalls.Add(query);
        return Task.FromResult(ListResponse);
    }

    public Task<Result<GetAccount.Item?>> GetAccount(GetAccount.Query query, CancellationToken cancellationToken)
    {
        if (Failure is not null) return Task.FromResult(Result.Fail<GetAccount.Item?>(Failure));

        var item = Find(query.TenantId, query.UserId) is { } user
            ? new GetAccount.Item
            {
                Email = user.Email.Value,
                Username = user.Username.Value,
                TermsAcceptedAtUtc = user.TermsAcceptedAtUtc?.UtcDateTime,
                RemainingRecoveryCodeCount = RemainingRecoveryCodes(user),
                EnabledFactors = user.MfaFactors
                    .Where(f => f.Enabled)
                    .OrderBy(f => f.EnrolledAtUtc)
                    .Select(f => new GetAccount.Factor
                    {
                        Id = f.Id,
                        FactorType = f.FactorType.ToString(),
                        EnrolledAtUtc = f.EnrolledAtUtc.UtcDateTime,
                        LastUsedAtUtc = f.LastUsedAtUtc?.UtcDateTime
                    })
                    .ToArray()
            }
            : null;

        return Task.FromResult(Result.Ok(item));
    }

    public Task<Result<GetForSignIn.Item?>> GetForSignIn(GetForSignIn.Query query, CancellationToken cancellationToken)
    {
        if (Failure is not null) return Task.FromResult(Result.Fail<GetForSignIn.Item?>(Failure));

        var item = Find(query.TenantId, query.UserId) is { } user
            ? new GetForSignIn.Item
            {
                Id = user.Id,
                Email = user.Email.Value,
                Username = user.Username.Value,
                Status = user.Status.ToString(),
                PendingActions = user.PendingActions.Select(a => a.ToString()).ToArray()
            }
            : null;

        return Task.FromResult(Result.Ok(item));
    }

    private User? Find(Guid tenantId, Guid userId) =>
        _users.FirstOrDefault(u => u.Id == userId && u.TenantId == tenantId && !u.IsDeleted);

    private static int RemainingRecoveryCodes(User user) => user.RecoveryCodes.Count(c => c.UsedAtUtc is null);
}
