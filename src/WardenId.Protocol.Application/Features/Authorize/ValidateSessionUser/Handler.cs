using FluentResults;
using MediatR;
using WardenId.Protocol.Domain;
using GetForSignInQuery = WardenId.Identity.Integration.Command.Features.Users.GetForSignIn.Query;

namespace WardenId.Protocol.Application.Features.Authorize.ValidateSessionUser;

/// <summary>
/// Re-checks the signed-in user at /authorize: a user disabled, locked or given a required action
/// after signing in must go back through login instead of getting a code that /token will refuse.
/// </summary>
public sealed class Handler(ISender sender) : IRequestHandler<Query, Result>
{
    public async Task<Result> Handle(Query request, CancellationToken cancellationToken)
    {
        var userResult = await sender.Send(new GetForSignInQuery(request.TenantId, request.UserId), cancellationToken);

        return userResult.IsSuccess && SignInUserRules.IsUsable(userResult.Value)
            ? Result.Ok()
            : Result.Fail(ProtocolErrors.InvalidGrant());
    }
}
