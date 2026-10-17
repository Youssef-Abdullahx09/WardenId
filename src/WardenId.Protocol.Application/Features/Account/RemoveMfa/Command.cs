using FluentResults;
using MediatR;

namespace WardenId.Protocol.Application.Features.Account.RemoveMfa;

/// <summary>A current code (authenticator app or recovery code), then every factor and recovery code is removed.</summary>
public sealed record Command(Guid TenantId, Guid UserId, string Code) : IRequest<Result>;
