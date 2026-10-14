using FluentResults;
using MediatR;
using WardenId.ClientManagement.Application.Abstractions;
using WardenId.ClientManagement.Domain;

namespace WardenId.ClientManagement.Application.Features.Clients.PostLogoutRedirectUris.Add;

public sealed class Handler(
    IClientRepository repository) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(
        Command request,
        CancellationToken cancellationToken)
    {
        // Post-logout URIs share the redirect URI rules: absolute, no fragment, no wildcard (exact match only).
        var uriResult = RedirectUri.Create(request.Uri);
        if (uriResult.IsFailed) return Result.Fail(uriResult.Errors);

        var clientResult = await repository.GetByIdAsync(request.TenantId, request.ClientId, cancellationToken);
        if (clientResult.IsFailed) return Result.Fail(clientResult.Errors);

        var client = clientResult.Value;

        client.AddPostLogoutRedirectUri(uriResult.Value);

        await repository.UpdateAsync(client, request.TenantId, cancellationToken);

        return Result.Ok();
    }
}
