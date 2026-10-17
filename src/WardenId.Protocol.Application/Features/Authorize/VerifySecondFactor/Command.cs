using FluentResults;
using MediatR;

namespace WardenId.Protocol.Application.Features.Authorize.VerifySecondFactor;

/// <summary>The sign-in code step: a TOTP code or a recovery code of the user whose password was just checked.</summary>
public sealed record Command(Guid TenantId, Guid UserId, string Code) : IRequest<Result<SecondFactorPassed>>;

/// <param name="Amr">How the session is authenticated, password included (<see cref="AuthenticationMethods"/>).</param>
public sealed record SecondFactorPassed(IReadOnlyList<string> Amr);
