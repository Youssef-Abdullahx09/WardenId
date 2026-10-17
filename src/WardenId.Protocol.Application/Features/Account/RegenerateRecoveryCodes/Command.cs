using FluentResults;
using MediatR;
using WardenId.Protocol.Application.Auditing;

namespace WardenId.Protocol.Application.Features.Account.RegenerateRecoveryCodes;

/// <summary>A current code from the authenticator app (not a recovery code), then a new set of recovery codes, shown once.</summary>
/// <param name="Sid">The login session the user is on, for the audit trail.</param>
public sealed record Command(Guid TenantId, Guid UserId, Guid Sid, string Code)
    : IRequest<Result<IReadOnlyList<string>>>, IActsAsUser;
