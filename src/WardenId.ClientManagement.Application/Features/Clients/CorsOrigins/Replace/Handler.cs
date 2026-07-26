using FluentResults;
using MediatR;
using WardenId.ClientManagement.Application.Abstractions;
using WardenId.ClientManagement.Domain;

namespace WardenId.ClientManagement.Application.Features.Clients.CorsOrigins.Replace;

public sealed class Handler(
    IClientRepository repository) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(
        Command request,
        CancellationToken cancellationToken)
    {
        var origins = new List<CorsOrigin>();
        foreach (var raw in request.Origins)
        {
            var originResult = CorsOrigin.Create(raw);
            if (originResult.IsFailed) return Result.Fail(originResult.Errors);

            origins.Add(originResult.Value);
        }

        var clientResult = await repository.GetByIdAsync(request.TenantId, request.ClientId, cancellationToken);
        if (clientResult.IsFailed) return Result.Fail(clientResult.Errors);

        var client = clientResult.Value;

        foreach (var existing in client.CorsOrigins.ToList())
        {
            client.RemoveCorsOrigin(existing);
        }

        foreach (var origin in origins)
        {
            client.AddCorsOrigin(origin);
        }

        await repository.UpdateAsync(client, request.TenantId, cancellationToken);

        return Result.Ok();
    }
}
