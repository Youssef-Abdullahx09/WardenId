using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WardenId.ClientManagement.Application.Abstractions;
using WardenId.ClientManagement.Domain;
using WardenId.ClientManagement.Integration.Command.Features.Clients.VerifySecret;

namespace WardenId.ClientManagement.Application.Features.Clients.VerifySecret;

/// <summary>
/// Verifies a presented client secret against every non-expired secret of a confidential,
/// enabled, shared-secret client. Every failure returns the same InvalidSecret error.
/// </summary>
public sealed class Handler(IClientManagementDbContext dbContext) : IRequestHandler<Query, Result>
{
    public async Task<Result> Handle(Query request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId) || string.IsNullOrEmpty(request.PresentedSecret))
            return Result.Fail(ClientErrors.InvalidSecret());

        var clientId = ClientId.FromTrustedSource(request.ClientId);

        var client = await dbContext.Clients
            .AsNoTracking()
            .Include(c => c.Secrets)
            .Where(c => c.TenantId == request.TenantId && c.ClientId == clientId && !c.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (client is null
            || !client.Enabled
            || client.ClientType != ClientType.Confidential
            || client.ClientAuthenticationMethod != ClientAuthenticationMethod.SharedSecret)
        {
            return Result.Fail(ClientErrors.InvalidSecret());
        }

        var now = DateTimeOffset.UtcNow;
        var liveHashes = client.Secrets
            .Where(s => !s.IsExpired(now))
            .Select(s => s.HashedValue);

        return ClientSecretHasher.Matches(request.PresentedSecret, liveHashes)
            ? Result.Ok()
            : Result.Fail(ClientErrors.InvalidSecret());
    }
}
