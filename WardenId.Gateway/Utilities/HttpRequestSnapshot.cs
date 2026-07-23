namespace WardenId.Gateway.Utilities;

public sealed record HttpRequestSnapshot(
    string Method,
    string Path,
    string? QueryString,
    IDictionary<string, string> Headers,
    object? Body,
    string? UserId,
    string? TraceId,
    string? IpAddress,
    string? UserAgent);