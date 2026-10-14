using FluentResults;
using MediatR;

namespace WardenId.Protocol.Application.Features.Sessions.ListForUser;

public sealed record Query(Guid TenantId, Guid UserId) : IRequest<Result<IReadOnlyList<UserSession>>>;
