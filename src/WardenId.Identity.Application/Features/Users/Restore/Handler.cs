using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;

namespace WardenId.Identity.Application.Features.Users.Restore;

/// <summary>Undoes a soft delete. The user comes back Disabled; an admin enables them to allow sign-in.</summary>
public sealed class Handler(IUserRepository repository) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var userResult = await repository.GetByIdIncludingDeletedAsync(request.TenantId, request.UserId, cancellationToken);
        if (userResult.IsFailed) return Result.Fail(userResult.Errors);

        var user = userResult.Value;

        var restoreResult = user.Restore();
        if (restoreResult.IsFailed) return restoreResult;

        await repository.RestoreAsync(user, request.ActorUserId, cancellationToken);

        return Result.Ok();
    }
}
