using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;

namespace WardenId.Identity.Application.Features.Users.Enable;

public sealed class Handler(IUserRepository repository) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var userResult = await repository.GetByIdAsync(request.TenantId, request.UserId, cancellationToken);
        if (userResult.IsFailed) return Result.Fail(userResult.Errors);

        var user = userResult.Value;

        var enableResult = user.Enable();
        if (enableResult.IsFailed) return enableResult;

        var saveResult = await repository.UpdateAsync(user, request.ActorUserId, cancellationToken);
        if (saveResult.IsFailed) return Result.Fail(saveResult.Errors);

        return Result.Ok();
    }
}
