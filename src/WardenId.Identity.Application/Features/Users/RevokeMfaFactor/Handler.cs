using FluentResults;
using MediatR;
using Microsoft.Extensions.Options;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.RevokeMfaFactor;

public sealed class Handler(
    IUserRepository repository,
    IOptions<IdentityPolicyOptions> options) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var userResult = await repository.GetByIdAsync(request.TenantId, request.UserId, cancellationToken);
        if (userResult.IsFailed) return Result.Fail(userResult.Errors);

        var user = userResult.Value;

        if (user.Status is UserStatus.Disabled or UserStatus.SoftDeleted)
            return Result.Fail(UserErrors.InvalidStatusTransition());

        var policy = MfaPolicy.Create(options.Value.MfaPolicyMode);

        var revokeResult = user.RevokeMfaFactor(request.FactorId, policy, request.TargetUserIsAdmin);
        if (revokeResult.IsFailed) return revokeResult;

        await repository.UpdateAsync(user, request.ActorUserId, cancellationToken);

        return Result.Ok();
    }
}
