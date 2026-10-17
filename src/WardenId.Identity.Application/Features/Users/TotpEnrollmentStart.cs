using System.Security.Cryptography;
using FluentResults;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Application.Mfa;
using WardenId.Identity.Domain;
using TotpEnrollment = WardenId.Identity.Integration.Command.Features.Users.BeginTotpEnrollment.TotpEnrollment;

namespace WardenId.Identity.Application.Features.Users;

/// <summary>Starts a TOTP enrollment on the user (not saved): a new secret, protected at rest, returned once.</summary>
internal static class TotpEnrollmentStart
{
    public static Result<TotpEnrollment> Begin(User user, string issuer, ITotpSecretProtector secretProtector, DateTimeOffset now)
    {
        var secret = Totp.GenerateSecret();
        try
        {
            var factor = MfaFactor.Create(MfaFactorType.Totp, secretProtector.Protect(secret), now);

            var beginResult = user.BeginMfaEnrollment(factor);
            if (beginResult.IsFailed) return Result.Fail(beginResult.Errors);

            var base32Secret = Totp.ToBase32(secret);
            var uri = Totp.OtpAuthUri(issuer.Trim(), user.Email.Value, base32Secret);
            return Result.Ok(new TotpEnrollment(factor.Id, base32Secret, uri));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }
}
