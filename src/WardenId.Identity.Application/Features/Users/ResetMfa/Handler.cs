using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;

namespace WardenId.Identity.Application.Features.Users.ResetMfa;

/// <summary>Not limited by the MFA policy: a required policy is exactly when a user who lost their authenticator needs it.</summary>
public sealed class Handler(IUserRepository repository) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var userResult = await repository.GetByIdAsync(request.TenantId, request.UserId, cancellationToken);
        if (userResult.IsFailed) return Result.Fail(userResult.Errors);
        var user = userResult.Value;

        var resetResult = user.ResetMfa();
        if (resetResult.IsFailed) return resetResult;

        var saveResult = await repository.UpdateAsync(user, request.ActorUserId, cancellationToken);
        if (saveResult.IsFailed) return Result.Fail(saveResult.Errors);

        return Result.Ok();
    }
}
