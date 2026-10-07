using System.Security.Cryptography;
using System.Text;
using FluentResults;
using MediatR;
using Microsoft.Extensions.Options;
using WardenId.Protocol.Application.Abstractions;
using WardenId.Protocol.Domain;
using AuthenticateCommand = WardenId.Identity.Integration.Command.Features.Users.Authenticate.Command;
using AuthenticatedUserStatus = WardenId.Identity.Integration.Command.Features.Users.Authenticate.UserStatus;
using AuthenticatedUserRequiredAction = WardenId.Identity.Integration.Command.Features.Users.Authenticate.RequiredAction;
using GetActiveForSigningQuery = WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetActiveForSigning.Query;

namespace WardenId.Protocol.Application.Features.Login;

public sealed class Handler(
    ISender sender,
    IJwtIssuer jwtIssuer,
    IRefreshTokenRepository refreshTokenRepository,
    IOptions<ProtocolTokenOptions> options) : IRequestHandler<Command, Result<LoginResult>>
{
    public async Task<Result<LoginResult>> Handle(Command request, CancellationToken cancellationToken)
    {
        var authResult = await sender.Send(
            new AuthenticateCommand(request.TenantId, request.EmailOrUsername, request.Password), cancellationToken);
        if (authResult.IsFailed) return Result.Fail<LoginResult>(authResult.Errors);

        var user = authResult.Value;
        if (user.Status != AuthenticatedUserStatus.Active || user.PendingActions.Count > 0)
            return Result.Ok(new LoginResult(RequiresAdditionalAction: true, Tokens: null));

        var keyResult = await sender.Send(new GetActiveForSigningQuery(request.TenantId), cancellationToken);
        if (keyResult.IsFailed) return Result.Fail<LoginResult>(keyResult.Errors);
        var tenantKey = keyResult.Value;
        var key = new ActiveSigningKey(tenantKey.KeyId, tenantKey.Algorithm, tenantKey.EncryptedPrivateKey, tenantKey.Slug);

        var now = DateTimeOffset.UtcNow;
        var accessLifetime = options.Value.AccessTokenLifetime;

        var accessToken = jwtIssuer.IssueAccessToken(key, request.TenantId, user.UserId, now, accessLifetime);
        var emailVerified = !user.PendingActions.Contains(AuthenticatedUserRequiredAction.VerifyEmail);
        var idToken = jwtIssuer.IssueIdToken(
            key, request.TenantId, user.UserId, user.Email, emailVerified, user.Username, now, options.Value.IdTokenLifetime);

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
