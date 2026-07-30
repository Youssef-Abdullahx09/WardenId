using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;

namespace WardenId.Identity.Application.Features.Users.VerifyMfaFactor;

public sealed class Handler(IUserRepository repository) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var userResult = await repository.GetByIdAsync(request.TenantId, request.UserId, cancellationToken);
        if (userResult.IsFailed) return Result.Fail(userResult.Errors);

        var user = userResult.Value;

        var verifyResult = user.VerifyMfaFactor(request.FactorId);
        if (verifyResult.IsFailed) return verifyResult;

        await repository.UpdateAsync(user, user.Id, cancellationToken);

        return Result.Ok();
    }
}
