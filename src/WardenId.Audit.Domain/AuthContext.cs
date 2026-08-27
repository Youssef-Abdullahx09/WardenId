namespace WardenId.Audit.Domain;

public sealed record AuthContext
{
    public string? Sid { get; }
    public string? Jti { get; }
    public string? Acr { get; }
    public IReadOnlyList<string> Amr { get; }
    public DateTimeOffset? AuthTime { get; }
    public string? Protocol { get; }

    private AuthContext(
        string? sid, string? jti, string? acr, IReadOnlyList<string> amr, DateTimeOffset? authTime, string? protocol)
    {
        Sid = sid;
        Jti = jti;
        Acr = acr;
        Amr = amr;
        AuthTime = authTime;
        Protocol = protocol;
    }

    public static AuthContext Create(
        string? sid = null, string? jti = null, string? acr = null,
        IReadOnlyList<string>? amr = null, DateTimeOffset? authTime = null, string? protocol = null)
        => new(sid, jti, acr, amr ?? [], authTime, protocol);

    public static AuthContext None() => new(null, null, null, [], null, null);
}
