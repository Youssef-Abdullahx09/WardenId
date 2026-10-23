using Microsoft.EntityFrameworkCore;

namespace WardenId.ClientManagement.Application.Tests.Fakes;

/// <summary>
/// <see cref="IClientRepository"/> over <see cref="TestClientManagementDbContext"/>. Mirrors the production
/// ClientRepository except for the SigningKeys include, because the test model ignores signing keys.
/// Records the audit actor of the last write so handlers can be checked for passing the caller's id.
/// </summary>
public sealed class InMemoryClientRepository(TestClientManagementDbContext dbContext) : IClientRepository
{
    public Guid? LastActorId { get; private set; }

    public async Task CreateAsync(Client client, Guid userId, CancellationToken cancellationToken = default)
    {
        LastActorId = userId;
        await dbContext.Clients.AddAsync(client, cancellationToken);
        await dbContext.SaveChangesAsync(userId, cancellationToken);
    }

    public async Task<Result<Client>> GetByIdAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default)
    {
        var client = await dbContext.Clients
            .Include(c => c.Secrets)
            .Where(c => c.Id == clientId && c.TenantId == tenantId && !c.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        return client is null ? Result.Fail(ClientErrors.NotFound) : client;
    }

    public async Task UpdateAsync(Client client, Guid userId, CancellationToken cancellationToken = default)
    {
        LastActorId = userId;
        await dbContext.SaveChangesAsync(userId, cancellationToken);
    }

    public async Task DeleteAsync(Client client, Guid userId, CancellationToken cancellationToken = default)
    {
        LastActorId = userId;
        await dbContext.SaveChangesAsync(userId, cancellationToken);
    }
}
