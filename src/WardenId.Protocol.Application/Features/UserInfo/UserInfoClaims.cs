namespace WardenId.Protocol.Application.Features.UserInfo;

/// <summary>What /userinfo returns: the subject plus the claims the access token's scopes release.</summary>
public sealed record UserInfoClaims(string Subject, ScopedUserClaims Claims);
