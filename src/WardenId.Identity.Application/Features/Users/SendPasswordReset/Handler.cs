using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.SendPasswordReset;

public sealed class Handler(
    IUserRepository repository,
    IIdentityEmailSender emailSender) : IRequestHandler<Command, Result>
{
    private readonly PasswordResetIssuer _issuer = new(repository, emailSender);

    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var userResult = await repository.GetByIdAsync(request.TenantId, request.UserId, cancellationToken);
        if (userResult.IsFailed) return Result.Fail(userResult.Errors);

        var user = userResult.Value;

        // Unlike the anonymous request, the caller is an authenticated admin, so saying why
        // nothing was sent leaks nothing.
        if (user.Status is UserStatus.Disabled or UserStatus.SoftDeleted)
            return Result.Fail(UserErrors.InvalidStatusTransition());

        await _issuer.IssueAsync(user, request.ActorUserId, cancellationToken);

        return Result.Ok();
    }
}
