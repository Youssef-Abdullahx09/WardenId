using FluentResults;
using WardenId.SharedKernel.Domain;

namespace WardenId.Protocol.Domain;

public sealed class RefreshToken : AggregateRoot
{
    // EF Core materialization only
    private RefreshToken() { }

    private RefreshToken(Guid tenantId, Guid userId, string tokenHash, DateTimeOffset expiresAtUtc)
    {
        TenantId = tenantId;
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public bool IsRevoked { get; private set; }

    public static Result<RefreshToken> Create(Guid tenantId, Guid userId, string tokenHash, DateTimeOffset expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(tokenHash)) return Result.Fail(ProtocolErrors.TokenHashRequired());
        if (expiresAtUtc <= DateTimeOffset.UtcNow) return Result.Fail(ProtocolErrors.RefreshTokenExpiryMustBeFuture());

        return Result.Ok(new RefreshToken(tenantId, userId, tokenHash, expiresAtUtc));
    }
}
