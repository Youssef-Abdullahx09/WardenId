using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.RequestEmailVerification;

public sealed class Handler(IUserRepository repository) : IRequestHandler<Command, Result<EmailVerificationRequested>>
{
    public async Task<Result<EmailVerificationRequested>> Handle(Command request, CancellationToken cancellationToken)
    {
        var userResult = await repository.GetByIdAsync(request.TenantId, request.UserId, cancellationToken);
        if (userResult.IsFailed) return Result.Fail(userResult.Errors);

        var user = userResult.Value;

        var (rawToken, tokenHash) = SecureTokenGenerator.Generate();
        var token = EmailVerificationToken.Create(tokenHash, DateTimeOffset.UtcNow.AddHours(24));
        user.AddEmailVerificationToken(token);

        await repository.UpdateAsync(user, user.Id, cancellationToken);

        return Result.Ok(new EmailVerificationRequested(rawToken));
    }
}
