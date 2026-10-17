using WardenId.Identity.Application.Features.Policies.UpdateIdentity;

namespace WardenId.Identity.Presentation.Features.Policies.UpdateIdentity;

/// <summary>
/// The tenant's whole override document, same shape as the GET response's settings. A null or
/// missing field (or section) clears that override, so the tenant uses the global value.
/// </summary>
public sealed record Request(
    PasswordOverrides? Password,
    LockoutOverrides? Lockout,
    MfaOverrides? Mfa,
    SelfRegistrationOverrides? SelfRegistration,
    TermsOverrides? Terms)
{
    public Command ToCommand(Guid tenantId, Guid actorUserId) =>
        new(tenantId, Password, Lockout, Mfa, SelfRegistration, Terms, actorUserId);
}
