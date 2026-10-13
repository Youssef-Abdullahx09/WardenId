using FluentResults;
using MediatR;
using WardenId.Protocol.Domain;
using GetForSignInQuery = WardenId.Identity.Integration.Command.Features.Users.GetForSignIn.Query;

namespace WardenId.Protocol.Application.Features.UserInfo;

/// <summary>
/// Claims fresh from Identity: a user disabled, locked or given a required action since the access token
/// was issued gets nothing (the endpoint answers invalid_token, same as a bad token — no state disclosure).
/// </summary>
public sealed class Handler(ISender sender) : IRequestHandler<Query, Result<UserInfoClaims>>
{
    public async Task<Result<UserInfoClaims>> Handle(Query request, CancellationToken cancellationToken)
    {
        var userResult = await sender.Send(new GetForSignInQuery(request.TenantId, request.UserId), cancellationToken);
        if (userResult.IsFailed || !SignInUserRules.IsUsable(userResult.Value))
            return Result.Fail<UserInfoClaims>(ProtocolErrors.InvalidGrant());

        var user = userResult.Value;
        return Result.Ok(new UserInfoClaims(user.UserId.ToString(), ScopedUserClaims.From(user, request.Scopes)));
    }
}
