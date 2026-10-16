using FluentResults;
using MediatR;

namespace WardenId.Protocol.Application.Features.Grants.RevokeForUser;

public sealed record Command(Guid TenantId, Guid UserId) : IRequest<Result>;
