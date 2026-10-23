using System.Globalization;
using WardenId.Identity.Application.Abstractions;
using WardenId.SharedKernel.Application.Abstractions;
using WardenId.SharedKernel.FileOperation;

namespace WardenId.Identity.Application;

internal sealed class IdentityEmailSender(
    IEmailService emailService,
    IReadFileService readFileService) : IIdentityEmailSender
{
    public Task SendEmailVerificationAsync(
        string toEmail, string username, string rawToken, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken) =>
        SendAsync(
            EmailTemplatePaths.EmailVerification, "Verify your email address",
            toEmail, username, EmailPlaceholderKeys.VerificationCode, rawToken, expiresAtUtc, cancellationToken);

    public Task SendPasswordResetAsync(
        string toEmail, string username, string rawToken, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken) =>
        SendAsync(
            EmailTemplatePaths.PasswordReset, "Reset your password",
            toEmail, username, EmailPlaceholderKeys.ResetCode, rawToken, expiresAtUtc, cancellationToken);

    private async Task SendAsync(
        string templatePath, string subject, string toEmail, string username,
        string codePlaceholderKey, string rawToken, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken)
    {
        var templateResult = await readFileService.ReadFileContentAsync(templatePath, cancellationToken);
        var template = templateResult.ValueOrDefault
            ?? throw new InvalidOperationException($"Email template not found: {templatePath}");

        var message = new EmailMessage(
            toEmail,
            subject,
            template,
            Placeholders:
            [
                new EmailPlaceholder(EmailPlaceholderKeys.Username, username),
                new EmailPlaceholder(codePlaceholderKey, rawToken),
                new EmailPlaceholder(EmailPlaceholderKeys.ExpiresAtUtc, expiresAtUtc.ToString("f", CultureInfo.InvariantCulture) + " UTC")
            ]);

        await emailService.SendAsync(message, cancellationToken);
    }
}
