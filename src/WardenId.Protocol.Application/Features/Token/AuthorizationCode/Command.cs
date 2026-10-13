using FluentResults;
using MediatR;

namespace WardenId.Protocol.Application.Features.Token.AuthorizationCode;

public sealed record Command(
    Guid TenantId, string Slug, string ClientId, Guid UserId, IReadOnlyCollection<string> Scopes, DateTimeOffset AuthTime)
    : IRequest<Result<UserTokenGrant>>;
