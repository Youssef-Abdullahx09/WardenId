using System.Xml;
using FluentResults;
using MediatR;
using Microsoft.Extensions.Options;

namespace WardenId.Protocol.Application.Features.Policies.GetTokens;

public sealed class Handler(
    IOptions<ProtocolTokenOptions> tokenOptions,
    IOptions<ProtocolSessionOptions> sessionOptions) : IRequestHandler<Query, Result<TokenPolicy>>
{
    public const string GlobalScope = "global";

    public Task<Result<TokenPolicy>> Handle(Query request, CancellationToken cancellationToken)
    {
        var tokens = tokenOptions.Value;
        var sessions = sessionOptions.Value;

        var result = new TokenPolicy(
            XmlConvert.ToString(tokens.AccessTokenLifetime),
            XmlConvert.ToString(tokens.IdTokenLifetime),
            XmlConvert.ToString(tokens.RefreshTokenLifetime),
            XmlConvert.ToString(tokens.RefreshTokenAbsoluteLifetime),
            XmlConvert.ToString(tokens.AuthorizationCodeLifetime),
            XmlConvert.ToString(sessions.CookieLifetime),
            XmlConvert.ToString(sessions.AbsoluteLifetime),
            GlobalScope);

        return Task.FromResult(Result.Ok(result));
    }
}
