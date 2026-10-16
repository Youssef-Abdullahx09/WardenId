namespace WardenId.Tenancy.Domain;

/// <summary>
/// Active (signs new tokens, published) -> Retiring (no longer signs, still published in JWKS
/// so tokens it already signed keep verifying) -> Retired (not published). Exactly one key per
/// tenant is Active. Transitions are enforced on Tenant (RotateSigningKey, RetireSigningKey).
/// </summary>
public enum SigningKeyStatus
{
    Active = 1,
    Retiring = 2,
    Retired = 3
}
