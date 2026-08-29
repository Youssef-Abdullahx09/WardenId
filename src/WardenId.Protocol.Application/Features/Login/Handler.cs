using System.Security.Cryptography;
using System.Text;
using FluentResults;
using MediatR;
using Microsoft.Extensions.Options;
using WardenId.Protocol.Application.Abstractions;
using WardenId.Protocol.Domain;

namespace WardenId.Protocol.Application.Features.Login;

public sealed class Handler(
    IAuthenticateUsers authenticator,
    IActiveSigningKeyProvider signingKeyProvider,
    IJwtIssuer jwtIssuer,
    IRefreshTokenRepository refreshTokenRepository,
    IOptions<ProtocolTokenOptions> options) : IRequestHandler<Command, Result<LoginResult>>
{
    public async Task<Result<LoginResult>> Handle(Command request, CancellationToken cancellationToken)
    {
        var authResult = await authenticator.AuthenticateAsync(
            request.TenantId, request.EmailOrUsername, request.Password, cancellationToken);
        if (authResult.IsFailed) return Result.Fail<LoginResult>(authResult.Errors);

        var user = authResult.Value;
        if (!user.IsActive || user.HasPendingActions)
            return Result.Ok(new LoginResult(RequiresAdditionalAction: true, Tokens: null));

        var keyResult = await signingKeyProvider.GetActiveSigningKeyAsync(request.TenantId, cancellationToken);
        if (keyResult.IsFailed) return Result.Fail<LoginResult>(keyResult.Errors);
        var key = keyResult.Value;

        var now = DateTimeOffset.UtcNow;
        var accessLifetime = options.Value.AccessTokenLifetime;

        var accessToken = jwtIssuer.IssueAccessToken(key, request.TenantId, user.UserId, now, accessLifetime);
        var idToken = jwtIssuer.IssueIdToken(
            key, request.TenantId, user.UserId, user.Email, user.EmailVerified, user.Username, now, options.Value.IdTokenLifetime);

        var (rawRefreshToken, refreshTokenHash) = GenerateRefreshToken();
        var refreshTokenResult = RefreshToken.Create(
            request.TenantId, user.UserId, refreshTokenHash, now.Add(options.Value.RefreshTokenLifetime));
        if (refreshTokenResult.IsFailed) return Result.Fail<LoginResult>(refreshTokenResult.Errors);

        await refreshTokenRepository.CreateAsync(refreshTokenResult.Value, user.UserId, cancellationToken);

        var tokens = new TokenSet(accessToken, rawRefreshToken, idToken, "Bearer", (int)accessLifetime.TotalSeconds);
        return Result.Ok(new LoginResult(RequiresAdditionalAction: false, tokens));
    }

    private static (string Raw, string Hash) GenerateRefreshToken()
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (raw, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))));
    }
}
