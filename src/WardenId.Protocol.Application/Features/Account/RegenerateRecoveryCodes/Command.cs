using FluentResults;
using MediatR;

namespace WardenId.Protocol.Application.Features.Account.RegenerateRecoveryCodes;

/// <summary>A current code from the authenticator app (not a recovery code), then a new set of recovery codes, shown once.</summary>
public sealed record Command(Guid TenantId, Guid UserId, string Code) : IRequest<Result<IReadOnlyList<string>>>;
