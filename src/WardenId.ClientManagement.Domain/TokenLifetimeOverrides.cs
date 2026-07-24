using FluentResults;
using WardenId.SharedKernel.Domain;

namespace WardenId.ClientManagement.Domain;

public sealed class TokenLifetimeOverrides : ValueObject
{
    public int? AccessTokenSeconds { get; }
    public int? IdTokenSeconds { get; }
    public int? RefreshTokenSeconds { get; }

    private TokenLifetimeOverrides(int? accessTokenSeconds, int? idTokenSeconds, int? refreshTokenSeconds)
    {
        AccessTokenSeconds = accessTokenSeconds;
        IdTokenSeconds = idTokenSeconds;
        RefreshTokenSeconds = refreshTokenSeconds;
    }

    public static Result<TokenLifetimeOverrides> Create(
        int? accessTokenSeconds, int? idTokenSeconds, int? refreshTokenSeconds)
    {
        if (accessTokenSeconds is <= 0)
            return Result.Fail(ClientErrors.TokenLifetimeMustBePositive(nameof(accessTokenSeconds)));

        if (idTokenSeconds is <= 0)
            return Result.Fail(ClientErrors.TokenLifetimeMustBePositive(nameof(idTokenSeconds)));

        if (refreshTokenSeconds is <= 0)
            return Result.Fail(ClientErrors.TokenLifetimeMustBePositive(nameof(refreshTokenSeconds)));

        return Result.Ok(new TokenLifetimeOverrides(accessTokenSeconds, idTokenSeconds, refreshTokenSeconds));
    }

    public static TokenLifetimeOverrides FromTrustedSource(
        int? accessTokenSeconds, int? idTokenSeconds, int? refreshTokenSeconds)
        => new(accessTokenSeconds, idTokenSeconds, refreshTokenSeconds);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return AccessTokenSeconds;
        yield return IdTokenSeconds;
        yield return RefreshTokenSeconds;
    }
}
