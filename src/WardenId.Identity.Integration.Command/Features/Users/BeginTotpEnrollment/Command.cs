using FluentResults;
using MediatR;

namespace WardenId.Identity.Integration.Command.Features.Users.BeginTotpEnrollment;

/// <summary>
/// Starts a TOTP enrollment: replaces any unverified TOTP factor of the user with a new one and returns its
/// secret once. <paramref name="Issuer"/> is the name shown in the authenticator app.
/// </summary>
public sealed record Command(Guid TenantId, Guid UserId, string Issuer) : IRequest<Result<TotpEnrollment>>;

/// <param name="Secret">Base32, no padding.</param>
/// <param name="OtpAuthUri">otpauth://totp/... for the QR code.</param>
public sealed record TotpEnrollment(Guid FactorId, string Secret, string OtpAuthUri);
