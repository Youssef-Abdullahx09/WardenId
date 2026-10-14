using FluentResults;
using MediatR;

namespace WardenId.Protocol.Application.Features.Sessions.Start;

public sealed record Command(Guid TenantId, Guid UserId) : IRequest<Result<StartedSession>>;
