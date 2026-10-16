namespace WardenId.Audit.Integration.Command.Features.Events.Record;

/// <summary>An object the event affected. Ids are opaque: user id, sid, client_id, OpenIddict authorization id.</summary>
public sealed record Target(TargetKind Kind, string Id)
{
    public static Target User(Guid userId) => new(TargetKind.User, userId.ToString());

    public static Target Session(Guid sid) => new(TargetKind.Session, sid.ToString());

    public static Target Client(string clientId) => new(TargetKind.OAuthClient, clientId);

    public static Target TokenFamily(string authorizationId) => new(TargetKind.TokenFamily, authorizationId);
}

public enum TargetKind
{
    User = 1,
    Session = 2,
    OAuthClient = 3,

    /// <summary>The grant (OpenIddict authorization) a refresh token rotation chain belongs to.</summary>
    TokenFamily = 4
}
