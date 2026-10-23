using FluentResults;
using MediatR;
using WardenId.Identity.Application.Utilities;
using WardenId.Identity.Domain;
using WardenId.Identity.Query.Contracts;
using UserQuery = WardenId.Identity.Query.Contracts.Features.Users.Get.Query;

namespace WardenId.Identity.Application.Features.Users.Get;

/// <summary>Read side: the user comes from the Dapper query layer (Identity.Query), not the aggregate.</summary>
public sealed class Handler(IUsersQuery usersQuery) : IRequestHandler<Query, Result<UserDetail>>
{
    public async Task<Result<UserDetail>> Handle(Query request, CancellationToken cancellationToken)
    {
        var result = await usersQuery.Get(new UserQuery(request.TenantId, request.UserId), cancellationToken);
        if (result.IsFailed) return Result.Fail(result.Errors);

        var user = result.Value;
        if (user is null) return Result.Fail(UserErrors.NotFound);

        var detail = new UserDetail(
            user.Id,
            user.Email,
            user.Username,
            user.FirstName,
            user.LastName,
            QueryColumns.ParseEnum<UserStatus>(user.Status),
            user.FailedAttemptCount,
            QueryColumns.AsUtc(user.LockedUntilUtc),
            user.PendingActions.Select(QueryColumns.ParseEnum<RequiredAction>).ToArray(),
            user.Attributes
                .Select(a => new UserAttributeSummary(a.Key, a.Value, QueryColumns.ParseEnum<UserAttributeValueType>(a.ValueType)))
                .ToArray(),
            user.MfaFactors
                .OrderBy(f => f.EnrolledAtUtc)
                .Select(f => new MfaFactorSummary(
                    f.Id,
                    QueryColumns.ParseEnum<MfaFactorType>(f.FactorType),
                    f.Enabled,
                    QueryColumns.AsUtc(f.EnrolledAtUtc),
                    QueryColumns.AsUtc(f.VerifiedAtUtc),
                    QueryColumns.AsUtc(f.LastUsedAtUtc)))
                .ToArray(),
            user.RemainingRecoveryCodeCount,
            QueryColumns.AsUtc(user.TermsAcceptedAtUtc),
            QueryColumns.AsUtc(user.CreatedAt));

        return Result.Ok(detail);
    }
}
