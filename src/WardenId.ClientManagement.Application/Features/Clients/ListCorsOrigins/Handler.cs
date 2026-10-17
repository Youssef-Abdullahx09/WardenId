using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WardenId.ClientManagement.Application.Abstractions;
using WardenId.ClientManagement.Integration.Command.Features.Clients.ListCorsOrigins;

namespace WardenId.ClientManagement.Application.Features.Clients.ListCorsOrigins;

/// <summary>Disabled clients are left out: they can't obtain tokens, so their origins have nothing to call.</summary>
public sealed class Handler(IClientManagementDbContext dbContext) : IRequestHandler<Query, Result<IReadOnlyList<string>>>
{
    public async Task<Result<IReadOnlyList<string>>> Handle(Query request, CancellationToken cancellationToken)
    {
        var clients = await dbContext.Clients
            .AsNoTracking()
            .Where(c => c.TenantId == request.TenantId && c.Enabled && !c.IsDeleted)
            .ToListAsync(cancellationToken);

        IReadOnlyList<string> origins = clients
            .SelectMany(c => c.CorsOrigins)
            .Select(o => o.Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        return Result.Ok(origins);
    }
}
