using FluentResults;
using MediatR;

namespace WardenId.Protocol.Application.Features.Authorize.BeginTotpEnrollment;

/// <summary>
/// Starts setting up an authenticator app during sign-in. <paramref name="Issuer"/> is the name the app shows.
/// </summary>
public sealed record Command(Guid TenantId, Guid UserId, string Issuer) : IRequest<Result<StartedTotpEnrollment>>;

/// <param name="Secret">Base32, no padding: shown for manual entry, never logged.</param>
/// <param name="OtpAuthUri">otpauth://totp/... for the QR code.</param>
public sealed record StartedTotpEnrollment(Guid FactorId, string Secret, string OtpAuthUri);
