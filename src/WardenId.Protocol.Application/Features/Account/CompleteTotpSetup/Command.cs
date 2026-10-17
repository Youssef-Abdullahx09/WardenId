using FluentResults;
using MediatR;
using WardenId.Protocol.Application.Auditing;

namespace WardenId.Protocol.Application.Features.Account.CompleteTotpSetup;

/// <summary>
/// The first code from an authenticator app set up (or replacing the old one) on the account page. Returns the
/// new recovery codes, shown once.
/// </summary>
/// <param name="Sid">The login session the user is on: kept when a replacement ends the user's other sessions.</param>
public sealed record Command(Guid TenantId, Guid UserId, Guid Sid, Guid FactorId, string Code)
    : IRequest<Result<IReadOnlyList<string>>>, IActsAsUser;
