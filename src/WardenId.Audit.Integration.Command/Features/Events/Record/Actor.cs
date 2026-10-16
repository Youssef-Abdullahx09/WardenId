namespace WardenId.Audit.Integration.Command.Features.Events.Record;

/// <summary>Who acted. Id is opaque (user id, client_id, component name); null only for <see cref="ActorKind.Anonymous"/>. Never an email or username.</summary>
public sealed record Actor(ActorKind Kind, string? Id)
{
    public static Actor Anonymous { get; } = new(ActorKind.Anonymous, null);

    public static Actor User(Guid userId) => new(ActorKind.EndUser, userId.ToString());

    public static Actor Client(string clientId) => new(ActorKind.OAuthClient, clientId);

    public static Actor System(string component) => new(ActorKind.System, component);
}

public enum ActorKind
{
    EndUser = 1,
    OAuthClient = 2,
    System = 3,
    Anonymous = 4
}
