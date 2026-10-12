using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;
using WardenId.Identity.Integration.Command.Features.Users.GetForSignIn;
using Query = WardenId.Identity.Integration.Command.Features.Users.GetForSignIn.Query;

namespace WardenId.Identity.Application.Features.Users.GetForSignIn;

public sealed class Handler(IUserRepository repository) : IRequestHandler<Query, Result<SignInUser>>
{
    public async Task<Result<SignInUser>> Handle(Query request, CancellationToken cancellationToken)
    {
        var userResult = await repository.GetByIdAsync(request.TenantId, request.UserId, cancellationToken);
        if (userResult.IsFailed) return Result.Fail(UserErrors.NotFound);

        var user = userResult.Value;
        if (user.Status == UserStatus.SoftDeleted) return Result.Fail(UserErrors.NotFound);

        return Result.Ok(new SignInUser(
            user.Id,
            user.Email.Value,
            user.Username.Value,
            ContractMapping.MapStatus(user.Status),
            user.PendingActions.Select(ContractMapping.MapRequiredAction).ToArray()));
    }
}
