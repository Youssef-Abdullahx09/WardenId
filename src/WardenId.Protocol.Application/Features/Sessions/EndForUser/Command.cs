using FluentResults;
using MediatR;
using WardenId.Protocol.Domain.Sessions;

namespace WardenId.Protocol.Application.Features.Sessions.EndForUser;

/// <param name="TenantId">Admin endpoint filter; Identity-triggered ends pass null (user ids are globally unique).</param>
/// <param name="ExceptSid">A session left alone: the one the user made the change from (an authenticator replaced or removed).</param>
public sealed record Command(Guid UserId, EndReason Reason, Guid? TenantId, Guid? ExceptSid = null) : IRequest<Result>;
