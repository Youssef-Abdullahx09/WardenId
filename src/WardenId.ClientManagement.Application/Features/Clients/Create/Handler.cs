using FluentResults;
using MediatR;
using WardenId.ClientManagement.Application.Abstractions;
using WardenId.ClientManagement.Domain;

namespace WardenId.ClientManagement.Application.Features.Clients.Create;

public sealed class Handler(
    IClientRepository repository) : IRequestHandler<Command, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        Command request,
        CancellationToken cancellationToken)
    {
        var clientResult = Client.Create(
            request.TenantId,
            request.ClientType,
            request.DisplayName,
            request.GrantType,
            request.ClientAuthenticationMethod,
            request.IsFirstParty,
            request.ConsentRequired,
            request.Description);

        if (clientResult.IsFailed) return Result.Fail(clientResult.Errors);

        var client = clientResult.Value;

        await repository.CreateAsync(client, request.ActorId, cancellationToken);

        return Result.Ok(client.Id);
    }
}
