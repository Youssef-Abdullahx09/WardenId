using FluentResults;
using WardenId.ClientManagement.Domain;

namespace WardenId.ClientManagement.Application.Abstractions;

public interface IClientRepository
{
    Task CreateAsync(
        Client client,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result<Client>> GetByIdAsync(
        Guid tenantId,
        Guid clientId,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Client client,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Client client,
        Guid userId,
        CancellationToken cancellationToken = default);
}
