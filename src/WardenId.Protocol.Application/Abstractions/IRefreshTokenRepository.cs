using WardenId.Protocol.Domain;

namespace WardenId.Protocol.Application.Abstractions;

public interface IRefreshTokenRepository
{
    Task CreateAsync(RefreshToken refreshToken, Guid userId, CancellationToken cancellationToken);
}
