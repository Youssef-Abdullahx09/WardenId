using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Application.Utilities;

namespace WardenId.Identity.Application.Features.Users.VerifyEmail;

public sealed class Handler(IUserRepository repository) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var userResult = await repository.GetByIdAsync(request.TenantId, request.UserId, cancellationToken);
        if (userResult.IsFailed) return Result.Fail(userResult.Errors);

        var user = userResult.Value;
        var tokenHash = SecureTokenGenerator.Hash(request.RawToken);

        var verifyResult = user.VerifyEmail(tokenHash);
        if (verifyResult.IsFailed) return verifyResult;

        var saveResult = await repository.UpdateAsync(user, user.Id, cancellationToken);
        if (saveResult.IsFailed) return Result.Fail(saveResult.Errors);

        return Result.Ok();
    }
}
