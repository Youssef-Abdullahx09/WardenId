using Microsoft.EntityFrameworkCore;
using WardenId.Protocol.Domain.Sessions;
using WardenId.SharedKernel.Application.Abstractions;

namespace WardenId.Protocol.Application.Abstractions;

public interface IProtocolDbContext : IDbContext
{
    DbSet<Session> Sessions { get; }
}
