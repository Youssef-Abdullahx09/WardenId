using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.RemoveAttribute;

public sealed class Handler(IUserRepository repository) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var userResult = await repository.GetByIdAsync(request.TenantId, request.UserId, cancellationToken);
        if (userResult.IsFailed) return Result.Fail(userResult.Errors);

        var user = userResult.Value;

        if (user.Status is UserStatus.Disabled or UserStatus.SoftDeleted)
            return Result.Fail(UserErrors.InvalidStatusTransition());

        var removeResult = user.RemoveAttribute(request.Key);
        if (removeResult.IsFailed) return removeResult;

        var saveResult = await repository.UpdateAsync(user, request.ActorUserId, cancellationToken);
        if (saveResult.IsFailed) return Result.Fail(saveResult.Errors);

        return Result.Ok();
    }
}
