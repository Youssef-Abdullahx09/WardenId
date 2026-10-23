namespace WardenId.Audit.Domain;

/// <summary>
/// Fixed segments of the audit log's serialized forms. These are hashed and signed into the
/// transparency log, so a value must never change.
/// </summary>
public static class AuditFormats
{
    /// <summary>Scope segments of a serialized actor and a checkpoint's scope label.</summary>
    public static class Scopes
    {
        public const string Platform = "platform";
        public const string Tenant = "tenant";
    }

    public static class Actors
    {
        /// <summary>The id of an actor with no known subject.</summary>
        public const string AnonymousId = "anonymous";
    }

    /// <summary>Origin lines of the signed notes.</summary>
    public static class SignedNotes
    {
        public const string CheckpointOrigin = "warden-audit";
        public const string KeyRotationOrigin = "warden-audit-key-rotation";
    }

    /// <summary>Patterns taxonomy keys must match.</summary>
    public static class KeyPatterns
    {
        /// <summary>Lowercase snake_case, e.g. "oauth_client".</summary>
        public const string SnakeCase = "^[a-z][a-z0-9_]*$";

        /// <summary>Lowercase dotted segments, e.g. "user.session.start".</summary>
        public const string Dotted = @"^[a-z0-9]+(\.[a-z0-9]+)+$";
    }
}
