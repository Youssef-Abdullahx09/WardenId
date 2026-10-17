using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WardenId.ClientManagement.Application.Abstractions;
using WardenId.ClientManagement.Domain;
using WardenId.ClientManagement.Integration.Command.Features.Clients.GetReference;

namespace WardenId.ClientManagement.Application.Features.Clients.GetReference;

public sealed class Handler(IClientManagementDbContext dbContext) : IRequestHandler<Query, Result<ClientReference>>
{
    public async Task<Result<ClientReference>> Handle(Query request, CancellationToken cancellationToken)
    {
        var client = await dbContext.Clients
            .AsNoTracking()
            .Where(c => c.TenantId == request.TenantId && c.Id == request.Id && !c.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        return client is null
            ? Result.Fail(ClientErrors.NotFound)
            : Result.Ok(new ClientReference(client.Id, client.ClientId.Value));
    }
}
