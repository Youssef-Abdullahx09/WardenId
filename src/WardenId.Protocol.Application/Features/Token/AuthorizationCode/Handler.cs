using FluentResults;
using MediatR;

namespace WardenId.Protocol.Application.Features.Token.AuthorizationCode;

/// <summary>Code redemption: the code's user, scopes and auth_time through the shared grant factory.</summary>
public sealed class Handler(UserTokenGrantFactory factory) : IRequestHandler<Command, Result<UserTokenGrant>>
{
    public Task<Result<UserTokenGrant>> Handle(Command request, CancellationToken cancellationToken) =>
        factory.CreateAsync(
            request.TenantId, request.Slug, request.ClientId, request.UserId,
            request.Scopes, request.Scopes, request.AuthTime, cancellationToken);
}
