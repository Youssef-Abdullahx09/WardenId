using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;
using Command = WardenId.Identity.Integration.Command.Features.Users.AcceptTerms.Command;

namespace WardenId.Identity.Application.Features.Users.AcceptTerms;

/// <summary>
/// Records the acceptance and clears AcceptTos. A save that loses a race with another change to the user (a sign-in
/// step, say) reloads and tries again, so the acceptance isn't lost.
/// </summary>
public sealed class Handler(IUserRepository repository, TimeProvider timeProvider) : IRequestHandler<Command, Result>
{
    internal const int MaxAttempts = 3;

    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var userResult = await repository.GetByIdAsync(request.TenantId, request.UserId, cancellationToken);
            if (userResult.IsFailed) return Result.Fail(userResult.Errors);
            var user = userResult.Value;

            var acceptResult = user.AcceptTerms(timeProvider.GetUtcNow());
            if (acceptResult.IsFailed) return acceptResult;

            var saveResult = await repository.UpdateAsync(user, user.Id, cancellationToken);
            if (saveResult.IsSuccess || attempt == MaxAttempts) return saveResult;
        }
    }
}
