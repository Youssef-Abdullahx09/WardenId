using FluentResults;
using WardenId.Tenancy.Domain;

namespace WardenId.Tenancy.Application.Abstractions;

public interface ITenantRepository
{
    Task CreateAsync(
        Tenant tenant,
        Guid userId,
        CancellationToken cancellationToken = default);
    
    Task<Result<Tenant>> GetByIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Tenant tenant,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result<Tenant>> GetBySlugAsync(
        Slug slug,
        CancellationToken cancellationToken = default);
    
    Task<bool> ExistsBySlugAsync(
        Slug slug,
        CancellationToken cancellationToken = default);
}