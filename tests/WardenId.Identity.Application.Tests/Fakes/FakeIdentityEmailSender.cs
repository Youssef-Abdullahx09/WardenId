namespace WardenId.Identity.Application.Tests.Fakes;

public sealed class FakeIdentityEmailSender : IIdentityEmailSender
{
    public List<(string ToEmail, string Username, string RawToken, DateTimeOffset ExpiresAtUtc)> SentVerificationEmails { get; } = [];
    public List<(string ToEmail, string Username, string RawToken, DateTimeOffset ExpiresAtUtc)> SentPasswordResetEmails { get; } = [];

    public Task SendEmailVerificationAsync(
        string toEmail, string username, string rawToken, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken)
    {
        SentVerificationEmails.Add((toEmail, username, rawToken, expiresAtUtc));
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(
        string toEmail, string username, string rawToken, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken)
    {
        SentPasswordResetEmails.Add((toEmail, username, rawToken, expiresAtUtc));
        return Task.CompletedTask;
    }
}
