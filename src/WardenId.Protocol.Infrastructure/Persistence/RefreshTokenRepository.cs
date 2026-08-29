using WardenId.Protocol.Application.Abstractions;
using WardenId.Protocol.Domain;

namespace WardenId.Protocol.Infrastructure.Persistence;

public sealed class RefreshTokenRepository(ProtocolDbContext dbContext) : IRefreshTokenRepository
{
    public async Task CreateAsync(RefreshToken refreshToken, Guid userId, CancellationToken cancellationToken)
    {
        await dbContext.RefreshTokens.AddAsync(refreshToken, cancellationToken);
        await dbContext.SaveChangesAsync(userId, cancellationToken);
    }
}
