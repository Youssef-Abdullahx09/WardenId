using FluentResults;
using MediatR;
using WardenId.Protocol.Application.Auditing;

namespace WardenId.Protocol.Application.Features.Account.RemoveMfa;

/// <summary>A current code (authenticator app or recovery code), then every factor and recovery code is removed.</summary>
/// <param name="Sid">The login session the user is on: kept when the user's other sessions end.</param>
public sealed record Command(Guid TenantId, Guid UserId, Guid Sid, string Code) : IRequest<Result>, IActsAsUser;
