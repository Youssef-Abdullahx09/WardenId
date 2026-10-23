using FluentResults;

namespace WardenId.Audit.Domain;

/// <summary>
/// Everything joins on this type. Id is opaque, tombstoned, and never reassigned — never
/// store email, username, or display name here. Rendering a human-readable name is a join
/// against a separately-tombstoned subject directory, performed at read time.
/// </summary>
public sealed record Actor
{
    public ActorType Type { get; }
    public string Id { get; }
    public ActorScope Scope { get; }
    public Actor? OnBehalfOf { get; }

    private Actor(ActorType type, string id, ActorScope scope, Actor? onBehalfOf)
    {
        Type = type;
        Id = id;
        Scope = scope;
        OnBehalfOf = onBehalfOf;
    }

    public static Result<Actor> Create(ActorType type, string id, ActorScope scope, Actor? onBehalfOf = null)
    {
        if (string.IsNullOrWhiteSpace(id)) return Result.Fail(AuditErrors.ActorIdRequired());
        return Result.Ok(new Actor(type, id, scope, onBehalfOf));
    }

    /// <summary>Pre-authentication events with no known subject — e.g. a failed login against an unknown username.</summary>
    public static Actor Anonymous(ActorScope scope) => new(ActorType.Anonymous, AuditFormats.Actors.AnonymousId, scope, null);

    /// <summary>
    /// Scope-qualified, unambiguous identifier for joins/indexing —
    /// "platform:adminuser:{id}" / "tenant:{tid}:oauthclient:{clientId}".
    /// A bare id that could be either a platform or a tenant actor is a footgun.
    /// </summary>
    public string ToSerializedForm()
    {
        var scopeSegment = Scope.IsPlatform ? AuditFormats.Scopes.Platform : $"{AuditFormats.Scopes.Tenant}:{Scope.TenantId}";
        var typeSegment = Type.ToString().ToLowerInvariant();
        return $"{scopeSegment}:{typeSegment}:{Id}";
    }
}
