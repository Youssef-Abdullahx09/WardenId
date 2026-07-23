namespace WardenId.SharedKernel.Infrastructure;

public sealed class SmtpSettings
{
    public required string Host { get; init; }
    public int Port { get; init; } = 587;
    public string? Username { get; init; }
    public string? Password { get; init; }
    public bool EnableSsl { get; init; } = true;
    public required string FromAddress { get; init; }
    public string? FromDisplayName { get; init; }
}
