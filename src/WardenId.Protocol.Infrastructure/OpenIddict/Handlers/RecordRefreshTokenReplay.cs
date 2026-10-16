using Microsoft.AspNetCore;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using WardenId.Audit.Integration.Command.Features.Events.Record;
using WardenId.Protocol.Application.Auditing;
using WardenId.SharedKernel.Application.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;
using RecordAuditEventCommand = WardenId.Audit.Integration.Command.Features.Events.Record.Command;

namespace WardenId.Protocol.Infrastructure.OpenIddict.Handlers;

/// <summary>
/// Audits refresh-token reuse. OpenIddict's ValidateTokenEntry (next) rejects a refresh token that was already
/// redeemed outside the reuse leeway (zero here) and revokes its whole family; it says nothing observable, so
/// this runs just before it and records the replay it is about to act on. Only at the token endpoint for a
/// refresh_token grant: introspecting or revoking a redeemed token is not a replay.
/// </summary>
public sealed class RecordRefreshTokenReplay(
    IOpenIddictTokenManager tokens, IAuthAuditTrail audit, ITenantContext tenantContext, TimeProvider timeProvider)
    : IOpenIddictServerHandler<ValidateTokenContext>
{
    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<ValidateTokenContext>()
            .UseScopedHandler<RecordRefreshTokenReplay>()
            .SetOrder(OpenIddictServerHandlers.Protection.ValidateTokenEntry.Descriptor.Order - 500)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    public async ValueTask HandleAsync(ValidateTokenContext context)
    {
        if (context.IsRejected
            || context.EndpointType is not OpenIddictServerEndpointType.Token
            || context.Transaction.Request is not { } request
            || !request.IsRefreshTokenGrantType()
            || request.ClientId is not { Length: > 0 } clientId
            || context.Principal is not { } principal
            || !principal.HasTokenType(TokenTypeIdentifiers.RefreshToken)
            || principal.GetTokenId() is not { Length: > 0 } tokenId
            || tenantContext.TenantId is not { } tenantId)
        {
            return;
        }

        var cancellationToken = context.Transaction.GetHttpRequest()?.HttpContext.RequestAborted ?? CancellationToken.None;

        if (await tokens.FindByIdAsync(tokenId, cancellationToken) is not { } token
            || !await tokens.HasStatusAsync(token, Statuses.Redeemed, cancellationToken))
        {
            return;
        }

        // Same rule as OpenIddict: a redemption inside the reuse leeway is a tolerated retry, not a replay.
        var now = timeProvider.GetUtcNow();
        if (context.Options.RefreshTokenReuseLeeway is { } leeway && leeway > TimeSpan.Zero
            && await tokens.GetRedemptionDateAsync(token, cancellationToken) is { } redeemedAt
            && redeemedAt + leeway >= now)
        {
            return;
        }

        await audit.RecordAsync(new RecordAuditEventCommand(
            SecurityEvent.RefreshTokenReplayed,
            tenantId,
            now,
            Actor.Client(clientId),
            TokenAuditFacts.Targets(clientId, principal),
            OutcomeReason.InvalidGrant,
            TokenAuditFacts.Session(principal, tokenId)));
    }
}
