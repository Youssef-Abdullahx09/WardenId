using FluentResults;
using MediatR;
using WardenId.ClientManagement.Application.Abstractions;
using WardenId.ClientManagement.Domain;

namespace WardenId.ClientManagement.Application.Features.Clients.RedirectUris.Add;

public sealed class Handler(
    IClientRepository repository) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(
        Command request,
        CancellationToken cancellationToken)
    {
        var uriResult = RedirectUri.Create(request.Uri);
        if (uriResult.IsFailed) return Result.Fail(uriResult.Errors);

        var clientResult = await repository.GetByIdAsync(request.TenantId, request.ClientId, cancellationToken);
        if (clientResult.IsFailed) return Result.Fail(clientResult.Errors);

        var client = clientResult.Value;

        client.AddRedirectUri(uriResult.Value);

        await repository.UpdateAsync(client, request.ActorId, cancellationToken);

        return Result.Ok();
    }
}
