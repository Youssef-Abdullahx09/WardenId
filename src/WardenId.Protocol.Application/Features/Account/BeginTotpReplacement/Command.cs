using FluentResults;
using MediatR;
using WardenId.Protocol.Application.Auditing;
using WardenId.Protocol.Application.Features.Authorize.BeginTotpEnrollment;

namespace WardenId.Protocol.Application.Features.Account.BeginTotpReplacement;

/// <summary>
/// A current code (authenticator app or recovery code), then a new authenticator app to set up. The old one keeps
/// working until the new one is confirmed with CompleteTotpSetup.
/// </summary>
/// <param name="Sid">The login session the user is on, for the audit trail.</param>
public sealed record Command(Guid TenantId, Guid UserId, Guid Sid, string Code, string Issuer)
    : IRequest<Result<StartedTotpEnrollment>>, IActsAsUser;
