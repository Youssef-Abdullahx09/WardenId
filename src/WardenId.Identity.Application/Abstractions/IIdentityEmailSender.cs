namespace WardenId.Identity.Application.Abstractions;

public interface IIdentityEmailSender
{
    Task SendEmailVerificationAsync(
        string toEmail, string username, string rawToken, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken);

    Task SendPasswordResetAsync(
        string toEmail, string username, string rawToken, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken);
}
