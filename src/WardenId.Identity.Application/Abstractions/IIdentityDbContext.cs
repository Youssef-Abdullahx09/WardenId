using Microsoft.EntityFrameworkCore;
using WardenId.Identity.Domain;
using WardenId.SharedKernel.Application.Abstractions;

namespace WardenId.Identity.Application.Abstractions;

public interface IIdentityDbContext : IDbContext
{
    DbSet<User> Users { get; }
    DbSet<IdentityTenantPolicy> TenantPolicies { get; }
}
