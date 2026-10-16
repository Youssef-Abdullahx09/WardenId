namespace WardenId.Audit.Integration.Command.Features.Events.Record;

public enum SecurityEvent
{
    SignInFailed = 1,
    SessionStarted = 2,
    SignedOut = 3,
    SessionEnded = 4,
    TokenIssuedAuthorizationCode = 5,
    TokenIssuedRefreshToken = 6,
    TokenIssuedClientCredentials = 7,
    TokenRevoked = 8,
    RefreshTokenReplayed = 9
}
