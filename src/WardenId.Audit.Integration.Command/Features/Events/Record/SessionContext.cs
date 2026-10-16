namespace WardenId.Audit.Integration.Command.Features.Events.Record;

/// <summary>
/// The authentication context the event ran under. TokenId is a token identifier (jti or OpenIddict token
/// id) — never the token itself.
/// </summary>
public sealed record SessionContext(
    Guid? Sid = null,
    DateTimeOffset? AuthTime = null,
    IReadOnlyList<string>? Amr = null,
    string? TokenId = null);
