using Microsoft.AspNetCore.DataProtection;

namespace WardenId.Identity.Infrastructure.Tests;

public class DataProtectionTotpSecretProtectorTests
{
    private static readonly byte[] Secret = "12345678901234567890"u8.ToArray();

    [Fact]
    public void Protect_HidesTheSecret_AndUnprotectGivesItBack()
    {
        var protector = new DataProtectionTotpSecretProtector(new EphemeralDataProtectionProvider());

        var protectedSecret = protector.Protect(Secret);

        Assert.DoesNotContain(Convert.ToBase64String(Secret), protectedSecret);
        Assert.Equal(Secret, protector.TryUnprotect(protectedSecret));
    }

    [Fact]
    public void TryUnprotect_IsNull_ForAValueItDidNotProtect()
    {
        var provider = new EphemeralDataProtectionProvider();
        var protector = new DataProtectionTotpSecretProtector(provider);
        var otherPurpose = Convert.ToBase64String(provider.CreateProtector("another purpose").Protect(Secret));

        Assert.Null(protector.TryUnprotect("opaque-legacy-value"));
        Assert.Null(protector.TryUnprotect(otherPurpose));
    }
}
