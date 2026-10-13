using FluentResults;
using MediatR;

namespace WardenId.Protocol.Application.Features.UserInfo;

public sealed record Query(Guid TenantId, Guid UserId, IReadOnlyCollection<string> Scopes) : IRequest<Result<UserInfoClaims>>;
