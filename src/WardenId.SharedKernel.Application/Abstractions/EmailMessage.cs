using Microsoft.AspNetCore.Http;

namespace WardenId.SharedKernel.Application.Abstractions;

public sealed record EmailMessage(
    string To,
    string Subject,
    string Body,
    bool IsBodyHtml = true,
    IReadOnlyCollection<string>? Cc = null,
    IReadOnlyCollection<string>? Bcc = null,
    IReadOnlyCollection<IFormFile>? Attachments = null,
    IReadOnlyCollection<EmailPlaceholder>? Placeholders = null);