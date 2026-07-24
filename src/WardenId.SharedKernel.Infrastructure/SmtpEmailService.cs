using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using WardenId.SharedKernel.Application.Abstractions;

namespace WardenId.SharedKernel.Infrastructure;

internal sealed class SmtpEmailService(
    SmtpSettings smtpSettings) : IEmailService
{
    public async Task SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken)
    {
        using var mimeMessage = new MimeMessage();

        mimeMessage.From.Add(new MailboxAddress(smtpSettings.FromDisplayName, smtpSettings.FromAddress));
        mimeMessage.To.Add(MailboxAddress.Parse(message.To));

        if (message.Cc is { Count: > 0 })
        {
            mimeMessage.Cc.AddRange(message.Cc.Select(MailboxAddress.Parse));
        }

        if (message.Bcc is { Count: > 0 })
        {
            mimeMessage.Bcc.AddRange(message.Bcc.Select(MailboxAddress.Parse));
        }

        mimeMessage.Subject = message.Subject;

        var body = ApplyPlaceholders(message.Body, message.Placeholders);

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = message.IsBodyHtml ? body : null,
            TextBody = message.IsBodyHtml ? null : body,
        };

        if (message.Attachments is { Count: > 0 })
        {
            foreach (var attachment in message.Attachments)
            {
                await using var attachmentStream = attachment.OpenReadStream();
                await bodyBuilder.Attachments.AddAsync(
                    attachment.FileName,
                    attachmentStream,
                    ContentType.Parse(attachment.ContentType),
                    cancellationToken);
            }
        }

        mimeMessage.Body = bodyBuilder.ToMessageBody();

        using var smtpClient = new SmtpClient();

        await smtpClient.ConnectAsync(
            smtpSettings.Host,
            smtpSettings.Port,
            smtpSettings.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
            cancellationToken);
        smtpClient.AuthenticationMechanisms.Remove("XOAUTH2");

        try
        {
            if (!string.IsNullOrWhiteSpace(smtpSettings.Username))
            {
                await smtpClient.AuthenticateAsync(smtpSettings.Username, smtpSettings.Password ?? string.Empty, cancellationToken);
            }

            await smtpClient.SendAsync(mimeMessage, cancellationToken);
            await smtpClient.DisconnectAsync(true, cancellationToken);

        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private static string ApplyPlaceholders(
        string body,
        IReadOnlyCollection<EmailPlaceholder>? placeholders)
    {
        if (placeholders is not { Count: > 0 })
        {
            return body;
        }

        foreach (var placeholder in placeholders)
        {
            body = body.Replace(placeholder.Key, placeholder.Value, StringComparison.Ordinal);
        }

        return body;
    }
}
