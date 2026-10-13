using FluentResults;
using MediatR;

namespace WardenId.Protocol.Application.Features.Token.RefreshToken;

/// <summary>
/// Refresh redemption. OpenIddict has already enforced single use (leeway 0, family revoked on replay),
/// expiry and client binding; this re-checks user and client and recomputes claims and the clamped lifetime.
/// A narrower requested scope filters the claims only: the rotated refresh token keeps the original grant
/// (RFC 6749 §6), so the refresh lifetime is always decided by GrantedScopes.
/// </summary>
public sealed class Handler(UserTokenGrantFactory factory) : IRequestHandler<Command, Result<UserTokenGrant>>
{
    public Task<Result<UserTokenGrant>> Handle(Command request, CancellationToken cancellationToken) =>
        factory.CreateAsync(
            request.TenantId, request.Slug, request.ClientId, request.UserId,
            request.GrantedScopes,
            request.RequestedScopes.Count > 0 ? request.RequestedScopes : request.GrantedScopes,
            request.AuthTime, cancellationToken);
}
