using OpenIddict.Abstractions;
using OpenIddict.Server;
using WardenId.Audit.Integration.Command.Features.Events.Record;
using WardenId.Protocol.Application.Auditing;
using WardenId.SharedKernel.Application.Abstractions;
using static OpenIddict.Server.OpenIddictServerEvents;
using RecordAuditEventCommand = WardenId.Audit.Integration.Command.Features.Events.Record.Command;

namespace WardenId.Protocol.Infrastructure.OpenIddict.Handlers;

/// <summary>
/// Audits an RFC 7009 revocation that revoked something (after <see cref="RevokeTokenFamily"/>, so a refresh
/// token's family is gone by now). An unknown, invalid or already-revoked token is answered 200 by OpenIddict
/// but the request is rejected internally, so it records nothing.
/// </summary>
public sealed class RecordTokenRevocation(IAuthAuditTrail audit, ITenantContext tenantContext, TimeProvider timeProvider)
    : IOpenIddictServerHandler<HandleRevocationRequestContext>
{
    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<HandleRevocationRequestContext>()
            .UseScopedHandler<RecordTokenRevocation>()
            .SetOrder(RevokeTokenFamily.Descriptor.Order + 1)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    public async ValueTask HandleAsync(HandleRevocationRequestContext context)
    {
        if (context.IsRejected
            || context.GenericTokenPrincipal is not { } principal
            || context.Request.ClientId is not { Length: > 0 } clientId
            || tenantContext.TenantId is not { } tenantId)
        {
            return;
        }

        await audit.RecordAsync(new RecordAuditEventCommand(
            SecurityEvent.TokenRevoked,
            tenantId,
            timeProvider.GetUtcNow(),
            Actor.Client(clientId),
            TokenAuditFacts.Targets(clientId, principal),
            Session: TokenAuditFacts.Session(principal, principal.GetTokenId())));
    }
}
