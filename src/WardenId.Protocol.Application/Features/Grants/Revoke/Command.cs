using FluentResults;
using MediatR;

namespace WardenId.Protocol.Application.Features.Grants.Revoke;

public sealed record Command(Guid TenantId, Guid UserId, string AuthorizationId) : IRequest<Result>;
