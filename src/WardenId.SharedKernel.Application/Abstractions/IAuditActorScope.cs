namespace WardenId.SharedKernel.Application.Abstractions;

/// <summary>
/// Names who acts for the audit records written inside a scope, when the request itself carries no authenticated
/// principal. The sign-in steps after the password and the self-service account pages run Identity writes for a
/// user known only from a protected ticket or the login session cookie, so the request-level ambient audit context
/// (built from the access token's claims) is anonymous; inside this scope the admin.* capture of those writes names
/// the user, in the user's tenant. Implemented by the Audit module over its ambient audit context. The scope flows
/// with the async call chain (AsyncLocal), so it must wrap the awaited call, not just precede it.
/// </summary>
public interface IAuditActorScope
{
    /// <summary>Until disposed, audit capture is attributed to end user <paramref name="userId"/> of <paramref name="tenantId"/>.</summary>
    IDisposable BeginEndUser(Guid tenantId, Guid userId);
}
