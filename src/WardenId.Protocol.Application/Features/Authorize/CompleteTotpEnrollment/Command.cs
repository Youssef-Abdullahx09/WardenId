using FluentResults;
using MediatR;
using WardenId.Protocol.Application.Auditing;

namespace WardenId.Protocol.Application.Features.Authorize.CompleteTotpEnrollment;

/// <summary>The first code from the authenticator app set up by BeginTotpEnrollment.</summary>
public sealed record Command(Guid TenantId, Guid UserId, Guid FactorId, string Code)
    : IRequest<Result<CompletedTotpEnrollment>>, IActsAsUser;

/// <param name="RecoveryCodes">Shown to the user once, right now; Identity keeps only their hashes.</param>
public sealed record CompletedTotpEnrollment(IReadOnlyList<string> RecoveryCodes);
