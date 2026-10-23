namespace WardenId.SharedKernel.Domain.Utilities;

/// <summary>
/// Audit target type keys (<see cref="IAuditableAggregate.AuditTargetTypeKey"/>). Persisted in audit events and
/// registered in Audit's <c>AuditTargetType</c>; renaming one breaks that lookup.
/// </summary>
public static class AuditTargetTypeKeys
{
    public const string Tenant = "tenant";
    public const string User = "user";
    public const string Session = "session";
    public const string OAuthClient = "oauth_client";
    public const string TokenFamily = "token_family";
    public const string CheckpointSigningKey = "checkpoint_signing_key";
    public const string IdentityPolicy = "identity_policy";
}
