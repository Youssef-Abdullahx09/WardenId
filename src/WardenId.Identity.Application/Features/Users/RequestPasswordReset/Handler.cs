using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.RequestPasswordReset;

public sealed class Handler(IUserRepository repository) : IRequestHandler<Command, Result<PasswordResetRequested?>>
{
    public async Task<Result<PasswordResetRequested?>> Handle(Command request, CancellationToken cancellationToken)
    {
        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailed) return Result.Ok<PasswordResetRequested?>(null);

        var userResult = await repository.GetByEmailAsync(request.TenantId, emailResult.Value, cancellationToken);
        if (userResult.IsFailed) return Result.Ok<PasswordResetRequested?>(null);

        var user = userResult.Value;

        var (rawToken, tokenHash) = SecureTokenGenerator.Generate();
        var token = PasswordResetToken.Create(tokenHash, DateTimeOffset.UtcNow.AddHours(1));
        user.RequestPasswordReset(token);

        await repository.UpdateAsync(user, user.Id, cancellationToken);

        return Result.Ok<PasswordResetRequested?>(new PasswordResetRequested(rawToken));
    }
}
