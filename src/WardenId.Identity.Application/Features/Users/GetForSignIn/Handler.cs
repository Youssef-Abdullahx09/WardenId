using FluentResults;
using MediatR;
using WardenId.Identity.Application.Utilities;
using WardenId.Identity.Domain;
using WardenId.Identity.Integration.Command.Features.Users.GetForSignIn;
using WardenId.Identity.Query.Contracts;
using GetForSignInQuery = WardenId.Identity.Integration.Command.Features.Users.GetForSignIn.Query;
using SignInUserQuery = WardenId.Identity.Query.Contracts.Features.Users.GetForSignIn.Query;

namespace WardenId.Identity.Application.Features.Users.GetForSignIn;

/// <summary>Read side: the user comes from the Dapper query layer (Identity.Query), not the aggregate.</summary>
public sealed class Handler(IUsersQuery usersQuery) : IRequestHandler<GetForSignInQuery, Result<SignInUser>>
{
    public async Task<Result<SignInUser>> Handle(GetForSignInQuery request, CancellationToken cancellationToken)
    {
        var userResult = await usersQuery.GetForSignIn(new SignInUserQuery(request.TenantId, request.UserId), cancellationToken);
        if (userResult.IsFailed) return Result.Fail(userResult.Errors);

        var user = userResult.Value;
        if (user is null) return Result.Fail(UserErrors.NotFound);
        var status = QueryColumns.ParseEnum<UserStatus>(user.Status);
        if (status == UserStatus.SoftDeleted) return Result.Fail(UserErrors.NotFound);

        return Result.Ok(new SignInUser(
            user.Id,
            user.Email,
            user.Username,
            ContractMapping.MapStatus(status),
            user.PendingActions
                .Select(QueryColumns.ParseEnum<RequiredAction>)
                .Select(ContractMapping.MapRequiredAction)
                .ToArray()));
    }
}
