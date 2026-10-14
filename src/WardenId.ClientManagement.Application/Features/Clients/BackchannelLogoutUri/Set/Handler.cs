using FluentResults;
using MediatR;
using WardenId.ClientManagement.Application.Abstractions;
using DomainBackchannelLogoutUri = WardenId.ClientManagement.Domain.BackchannelLogoutUri;

namespace WardenId.ClientManagement.Application.Features.Clients.BackchannelLogoutUri.Set;

public sealed class Handler(
    IClientRepository repository) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(
        Command request,
        CancellationToken cancellationToken)
    {
        DomainBackchannelLogoutUri? uri = null;
        if (request.Uri is not null)
        {
            var uriResult = DomainBackchannelLogoutUri.Create(request.Uri);
            if (uriResult.IsFailed) return Result.Fail(uriResult.Errors);

            uri = uriResult.Value;
        }

        var clientResult = await repository.GetByIdAsync(request.TenantId, request.ClientId, cancellationToken);
        if (clientResult.IsFailed) return Result.Fail(clientResult.Errors);

        var client = clientResult.Value;

        client.SetBackchannelLogoutUri(uri);

        await repository.UpdateAsync(client, request.TenantId, cancellationToken);

        return Result.Ok();
    }
}
