namespace WardenId.Audit.Domain;

public enum ActorType
{
    EndUser = 1,
    AdminUser = 2,
    ServiceAccount = 3,
    OAuthClient = 4,
    System = 5,
    Anonymous = 6,

    /// <summary>
    /// Authenticated (subject claim present) but the finer-grained type cannot be resolved
    /// yet — no role/actor-type claim exists anywhere in the codebase today (login/token
    /// issuance isn't built). Assigned by the audit HTTP middleware; revisit once real
    /// role claims land rather than guessing EndUser vs AdminUser.
    /// </summary>
    AuthenticatedUnknown = 7
}
