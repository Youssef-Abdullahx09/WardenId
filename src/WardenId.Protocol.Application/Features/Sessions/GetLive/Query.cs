using FluentResults;
using MediatR;

namespace WardenId.Protocol.Application.Features.Sessions.GetLive;

public sealed record Query(Guid TenantId, Guid Sid) : IRequest<Result<LiveSession>>;
