using FluentResults;
using MediatR;
using Microsoft.Extensions.Options;
using WardenId.Protocol.Application.Features.Policies;
using GetForAuthenticationQuery = WardenId.ClientManagement.Integration.Command.Features.Clients.GetForAuthentication.Query;

namespace WardenId.Protocol.Application.Features.Token.ClientCredentials;

public sealed class Handler(ISender sender, IOptions<ProtocolTokenOptions> options, ITenantLifetimes tenantLifetimes)
    : IRequestHandler<Command, Result<ClientCredentialsGrant>>
{
    public async Task<Result<ClientCredentialsGrant>> Handle(Command request, CancellationToken cancellationToken)
    {
        var clientResult = await sender.Send(new GetForAuthenticationQuery(request.TenantId, request.ClientId), cancellationToken);
        if (clientResult.IsFailed) return Result.Fail<ClientCredentialsGrant>(clientResult.Errors);

        var settings = options.Value;
        var audience = settings.AudienceFor(request.Slug);
        // Client override ?? tenant override ?? global default.
        var lifetimes = await tenantLifetimes.GetAsync(request.TenantId, cancellationToken);
        var lifetime = lifetimes.AccessTokenFor(clientResult.Value.AccessTokenLifetimeSeconds);

        return Result.Ok(new ClientCredentialsGrant(request.ClientId, request.TenantId, audience, lifetime));
    }
}
