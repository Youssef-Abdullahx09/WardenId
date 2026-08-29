namespace WardenId.Protocol.Application.Features.Login;

public sealed record LoginResult(bool RequiresAdditionalAction, TokenSet? Tokens);
