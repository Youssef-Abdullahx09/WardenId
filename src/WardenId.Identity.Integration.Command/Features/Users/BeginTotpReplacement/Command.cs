using FluentResults;
using MediatR;
using WardenId.Identity.Integration.Command.Features.Users.BeginTotpEnrollment;

namespace WardenId.Identity.Integration.Command.Features.Users.BeginTotpReplacement;

/// <summary>
/// Checks a current code (TOTP or recovery code, same rules as VerifySecondFactor), then starts a new TOTP
/// enrollment. Completing it with CompleteTotpEnrollment removes the user's other TOTP factors.
/// </summary>
public sealed record Command(Guid TenantId, Guid UserId, string Code, string Issuer) : IRequest<Result<TotpEnrollment>>;
