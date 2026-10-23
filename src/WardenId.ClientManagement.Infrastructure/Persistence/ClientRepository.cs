using FluentResults;
using Microsoft.EntityFrameworkCore;
using WardenId.ClientManagement.Application.Abstractions;
using WardenId.ClientManagement.Domain;

namespace WardenId.ClientManagement.Infrastructure.Persistence;

public sealed class ClientRepository(
    IClientManagementDbContext dbContext) : IClientRepository
{
    public async Task CreateAsync(
        Client client,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Clients.AddAsync(client, cancellationToken);
        await dbContext.SaveChangesAsync(userId, cancellationToken);
    }

    public async Task<Result<Client>> GetByIdAsync(
        Guid tenantId,
        Guid clientId,
        CancellationToken cancellationToken = default)
    {
        var client = await dbContext.Clients
            .Include(c => c.Secrets)
            .Include(c => c.SigningKeys)
            .Where(c => c.Id == clientId && c.TenantId == tenantId && !c.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        return client is null ? Result.Fail(ClientErrors.NotFound) : client;
    }

    public async Task UpdateAsync(
        Client client,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(userId, cancellationToken);
    }

    public async Task DeleteAsync(
        Client client,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(userId, cancellationToken);
    }
}
