using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.RevokeMfaFactor;

public sealed class Handler(
    IUserRepository repository,
    IdentityPolicyResolver policies) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var userResult = await repository.GetByIdAsync(request.TenantId, request.UserId, cancellationToken);
        if (userResult.IsFailed) return Result.Fail(userResult.Errors);

        var user = userResult.Value;

        if (user.Status is UserStatus.Disabled or UserStatus.SoftDeleted)
            return Result.Fail(UserErrors.InvalidStatusTransition());

        var policyResult = await policies.ResolveAsync(request.TenantId, cancellationToken);
        if (policyResult.IsFailed) return Result.Fail(policyResult.Errors);
        var policy = policyResult.Value.Mfa;

        var revokeResult = user.RevokeMfaFactor(request.FactorId, policy);
        if (revokeResult.IsFailed) return revokeResult;

        await repository.UpdateAsync(user, request.ActorUserId, cancellationToken);

        return Result.Ok();
    }
}
