using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;

namespace WardenId.Identity.Application.Features.Users.SetAttribute;

public sealed class Handler(IUserRepository repository) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var userResult = await repository.GetByIdAsync(request.TenantId, request.UserId, cancellationToken);
        if (userResult.IsFailed) return Result.Fail(userResult.Errors);

        var user = userResult.Value;

        var setAttributeResult = user.SetAttribute(request.Key, request.Value, request.ValueType);
        if (setAttributeResult.IsFailed) return setAttributeResult;

        await repository.UpdateAsync(user, request.ActorUserId, cancellationToken);

        return Result.Ok();
    }
}
