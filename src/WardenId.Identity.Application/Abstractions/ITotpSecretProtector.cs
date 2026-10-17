namespace WardenId.Identity.Application.Abstractions;

/// <summary>
/// Protects a TOTP shared secret at rest. The protected form is what <c>MfaFactor.CredentialData</c> holds; the raw
/// secret is never stored, logged or audited.
/// </summary>
public interface ITotpSecretProtector
{
    string Protect(byte[] secret);

    /// <summary>
    /// The raw secret, or null when <paramref name="protectedSecret"/> isn't a secret this protector protected (such
    /// as a value stored by the admin enrollment endpoint that predates TOTP, which can't check any code).
    /// </summary>
    byte[]? TryUnprotect(string protectedSecret);
}
