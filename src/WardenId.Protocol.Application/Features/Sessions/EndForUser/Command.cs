using FluentResults;
using MediatR;
using WardenId.Protocol.Domain.Sessions;

namespace WardenId.Protocol.Application.Features.Sessions.EndForUser;

/// <param name="TenantId">Admin endpoint filter; Identity-triggered ends pass null (user ids are globally unique).</param>
public sealed record Command(Guid UserId, EndReason Reason, Guid? TenantId) : IRequest<Result>;
