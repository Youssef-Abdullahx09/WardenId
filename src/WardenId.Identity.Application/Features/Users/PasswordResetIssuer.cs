using FluentResults;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Application.Utilities;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users;

/// <summary>
/// Mints a password-reset token, saves it on the user and emails the raw token: shared by the
/// anonymous "forgot password" request and the admin-initiated reset, so both send the same email
/// with the same lifetime.
/// </summary>
internal sealed class PasswordResetIssuer(
    IUserRepository repository,
    IIdentityEmailSender emailSender)
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);

    /// <param name="actorUserId">Who asked for the reset: the user themselves, or an admin.</param>
    /// <returns>
    /// The raw token; it only ever leaves the process in the email. Fails, sending nothing, when a concurrent change
    /// to the user stopped the token from being saved.
    /// </returns>
    public async Task<Result<string>> IssueAsync(User user, Guid actorUserId, CancellationToken cancellationToken)
    {
        var (rawToken, tokenHash) = SecureTokenGenerator.Generate();
        var expiresAtUtc = DateTimeOffset.UtcNow.Add(TokenLifetime);
        user.RequestPasswordReset(PasswordResetToken.Create(tokenHash, expiresAtUtc));

        var saveResult = await repository.UpdateAsync(user, actorUserId, cancellationToken);
        if (saveResult.IsFailed) return Result.Fail(saveResult.Errors);

        await emailSender.SendPasswordResetAsync(user.Email.Value, user.Username.Value, rawToken, expiresAtUtc, cancellationToken);

        return Result.Ok(rawToken);
    }
}
