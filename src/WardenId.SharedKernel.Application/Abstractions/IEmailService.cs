namespace WardenId.SharedKernel.Application.Abstractions;

public interface IEmailService
{
    Task SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken);
}
