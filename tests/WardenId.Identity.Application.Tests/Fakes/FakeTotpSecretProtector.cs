namespace WardenId.Identity.Application.Tests.Fakes;

/// <summary>Reversible but not secret: "protected:" + base64. Anything else can't be unprotected.</summary>
public sealed class FakeTotpSecretProtector : ITotpSecretProtector
{
    private const string Prefix = "protected:";

    public string Protect(byte[] secret) => Prefix + Convert.ToBase64String(secret);

    public byte[]? TryUnprotect(string protectedSecret) =>
        protectedSecret.StartsWith(Prefix, StringComparison.Ordinal)
            ? Convert.FromBase64String(protectedSecret[Prefix.Length..])
            : null;
}
