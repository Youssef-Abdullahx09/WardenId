using FluentResults;
using MediatR;

namespace WardenId.Protocol.Application.Features.Account.CompleteTotpSetup;

/// <summary>
/// The first code from an authenticator app set up (or replacing the old one) on the account page. Returns the
/// new recovery codes, shown once.
/// </summary>
public sealed record Command(Guid TenantId, Guid UserId, Guid FactorId, string Code) : IRequest<Result<IReadOnlyList<string>>>;
