using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Application.Utilities;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.RequestEmailVerification;

public sealed class Handler(
    IUserRepository repository,
    IIdentityEmailSender emailSender) : IRequestHandler<Command, Result<EmailVerificationRequested>>
{
    public async Task<Result<EmailVerificationRequested>> Handle(Command request, CancellationToken cancellationToken)
    {
        var userResult = await repository.GetByIdAsync(request.TenantId, request.UserId, cancellationToken);
        if (userResult.IsFailed) return Result.Fail(userResult.Errors);

        var user = userResult.Value;

        if (user.Status is UserStatus.Disabled or UserStatus.SoftDeleted)
            return Result.Fail(UserErrors.InvalidStatusTransition());

        var (rawToken, tokenHash) = SecureTokenGenerator.Generate();
        var expiresAtUtc = DateTimeOffset.UtcNow.AddHours(24);
        var token = EmailVerificationToken.Create(tokenHash, expiresAtUtc);
        user.AddEmailVerificationToken(token);

        var saveResult = await repository.UpdateAsync(user, user.Id, cancellationToken);
        if (saveResult.IsFailed) return Result.Fail(saveResult.Errors);

        await emailSender.SendEmailVerificationAsync(user.Email.Value, user.Username.Value, rawToken, expiresAtUtc, cancellationToken);

        return Result.Ok(new EmailVerificationRequested(rawToken));
    }
}
