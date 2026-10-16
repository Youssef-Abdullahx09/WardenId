using FluentResults;
using MediatR;

namespace WardenId.Protocol.Application.Features.Sessions.ListForUser;

/// <param name="IncludeEnded">Also ended and expired sessions; otherwise live ones only.</param>
public sealed record Query(Guid TenantId, Guid UserId, bool IncludeEnded = false) : IRequest<Result<IReadOnlyList<UserSession>>>;
