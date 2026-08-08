using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.RequestPasswordReset;

public sealed class Handler(
    IUserRepository repository,
    IIdentityEmailSender emailSender) : IRequestHandler<Command, Result<PasswordResetRequested?>>
{
    public async Task<Result<PasswordResetRequested?>> Handle(Command request, CancellationToken cancellationToken)
    {
        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailed) return Result.Ok<PasswordResetRequested?>(null);

        var userResult = await repository.GetByEmailAsync(request.TenantId, emailResult.Value, cancellationToken);
        if (userResult.IsFailed) return Result.Ok<PasswordResetRequested?>(null);

        var user = userResult.Value;

        // Disabled/soft-deleted targets are treated exactly like "no such user" — minting a token or
        // returning a distinguishing failure would leak account state to an anonymous caller.
        if (user.Status is UserStatus.Disabled or UserStatus.SoftDeleted)
            return Result.Ok<PasswordResetRequested?>(null);

        var (rawToken, tokenHash) = SecureTokenGenerator.Generate();
        var expiresAtUtc = DateTimeOffset.UtcNow.AddHours(1);
        var token = PasswordResetToken.Create(tokenHash, expiresAtUtc);
        user.RequestPasswordReset(token);

        await repository.UpdateAsync(user, user.Id, cancellationToken);

        await emailSender.SendPasswordResetAsync(user.Email.Value, user.Username.Value, rawToken, expiresAtUtc, cancellationToken);

        return Result.Ok<PasswordResetRequested?>(new PasswordResetRequested(rawToken));
    }
}
