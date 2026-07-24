using Microsoft.EntityFrameworkCore;
using WardenId.ClientManagement.Domain;
using WardenId.SharedKernel.Application.Abstractions;

namespace WardenId.ClientManagement.Application.Abstractions;

public interface IClientManagementDbContext : IDbContext
{
    DbSet<Client> Clients { get; }
}
