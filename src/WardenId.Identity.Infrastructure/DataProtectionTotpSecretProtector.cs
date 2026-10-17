using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using WardenId.Identity.Application.Abstractions;

namespace WardenId.Identity.Infrastructure;

/// <summary>
/// Protects TOTP secrets with the app's single shared Data Protection key ring (set up by Tenancy), under this
/// module's own purpose, like Audit's checkpoint keys: the derived key is distinct from every other module's.
/// </summary>
public sealed class DataProtectionTotpSecretProtector(IDataProtectionProvider dataProtectionProvider) : ITotpSecretProtector
{
    internal const string DataProtectionPurpose = "WardenId.Identity.TotpSecret.v1";

    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(DataProtectionPurpose);

    public string Protect(byte[] secret) => Convert.ToBase64String(_protector.Protect(secret));

    public byte[]? TryUnprotect(string protectedSecret)
    {
        try
        {
            return _protector.Unprotect(Convert.FromBase64String(protectedSecret));
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return null;
        }
    }
}
