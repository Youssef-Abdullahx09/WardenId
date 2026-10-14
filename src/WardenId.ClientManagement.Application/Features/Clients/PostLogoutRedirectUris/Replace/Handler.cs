using FluentResults;
using MediatR;
using WardenId.ClientManagement.Application.Abstractions;
using WardenId.ClientManagement.Domain;

namespace WardenId.ClientManagement.Application.Features.Clients.PostLogoutRedirectUris.Replace;

public sealed class Handler(
    IClientRepository repository) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(
        Command request,
        CancellationToken cancellationToken)
    {
        // Validate the whole set before loading the client: one bad entry rejects the replace atomically.
        var uris = new List<RedirectUri>();
        foreach (var raw in request.Uris)
        {
            var uriResult = RedirectUri.Create(raw);
            if (uriResult.IsFailed) return Result.Fail(uriResult.Errors);

            uris.Add(uriResult.Value);
        }

        var clientResult = await repository.GetByIdAsync(request.TenantId, request.ClientId, cancellationToken);
        if (clientResult.IsFailed) return Result.Fail(clientResult.Errors);

        var client = clientResult.Value;

        client.ReplacePostLogoutRedirectUris(uris);

        await repository.UpdateAsync(client, request.TenantId, cancellationToken);

        return Result.Ok();
    }
}
