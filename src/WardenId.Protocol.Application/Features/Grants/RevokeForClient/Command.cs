using FluentResults;
using MediatR;

namespace WardenId.Protocol.Application.Features.Grants.RevokeForClient;

/// <param name="ClientId">The client's Guid id, as in ClientManagement's admin routes (not the public client_id).</param>
public sealed record Command(Guid TenantId, Guid ClientId) : IRequest<Result>;
