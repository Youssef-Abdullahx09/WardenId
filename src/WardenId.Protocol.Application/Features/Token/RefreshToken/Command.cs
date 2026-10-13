using FluentResults;
using MediatR;

namespace WardenId.Protocol.Application.Features.Token.RefreshToken;

/// <summary>
/// GrantedScopes are the refresh token's own (they decide whether a rotated refresh token is issued);
/// RequestedScopes is the optional narrower <c>scope</c> parameter (empty = not sent), used for claims only.
/// </summary>
public sealed record Command(
    Guid TenantId, string Slug, string ClientId, Guid UserId,
    IReadOnlyCollection<string> GrantedScopes, IReadOnlyCollection<string> RequestedScopes, DateTimeOffset AuthTime)
    : IRequest<Result<UserTokenGrant>>;
