using FluentResults;
using MediatR;
using WardenId.ClientManagement.Application.Abstractions;

namespace WardenId.ClientManagement.Application.Features.Clients.Secrets.Expire;

public sealed class Handler(
    IClientRepository repository) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(
        Command request,
        CancellationToken cancellationToken)
    {
        var clientResult = await repository.GetByIdAsync(request.TenantId, request.ClientId, cancellationToken);
        if (clientResult.IsFailed) return Result.Fail(clientResult.Errors);

        var client = clientResult.Value;

        var expireResult = client.ExpireSecret(request.SecretId);
        if (expireResult.IsFailed) return expireResult;

        await repository.UpdateAsync(client, request.ActorId, cancellationToken);

        return Result.Ok();
    }
}
