using Microsoft.EntityFrameworkCore;
using WardenId.SharedKernel.Application.Abstractions;
using WardenId.Tenancy.Domain;

namespace WardenId.Tenancy.Application.Abstractions;

public interface ITenancyDbContext: IDbContext
{
    public DbSet<Tenant> Tenants { get; }
}