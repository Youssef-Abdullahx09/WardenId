namespace WardenId.Identity.Domain;

// WebAuthnPasskey is the FR-ID-08 extension point — reserved, not implemented yet.
public enum MfaFactorType
{
    Totp = 1,
    WebAuthnPasskey = 2
}
