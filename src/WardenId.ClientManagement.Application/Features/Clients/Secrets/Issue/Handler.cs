using System.Security.Cryptography;
using FluentResults;
using MediatR;
using WardenId.ClientManagement.Application.Abstractions;
using WardenId.ClientManagement.Domain;

namespace WardenId.ClientManagement.Application.Features.Clients.Secrets.Issue;

public sealed class Handler(
    IClientRepository repository) : IRequestHandler<Command, Result<ClientSecretIssued>>
{
    private const int OverlapDaysDefault = 7;

    public async Task<Result<ClientSecretIssued>> Handle(
        Command request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Label)) return Result.Fail(ClientErrors.SecretLabelRequired());

        var clientResult = await repository.GetByIdAsync(request.TenantId, request.ClientId, cancellationToken);
        if (clientResult.IsFailed) return Result.Fail(clientResult.Errors);

        var client = clientResult.Value;

        var plainTextSecret = GeneratePlainTextSecret();
        var secret = ClientSecret.Create(ClientSecretHasher.Hash(plainTextSecret), request.Label.Trim(), expiresAt: null);

        var now = DateTimeOffset.UtcNow;
        var hasActiveSecret = client.Secrets.Any(s => !s.IsExpired(now));

        var addResult = hasActiveSecret
            ? client.RotateSecret(secret, now.AddDays(request.OverlapDays ?? OverlapDaysDefault))
            : client.AddSecret(secret);

        if (addResult.IsFailed) return Result.Fail(addResult.Errors);

        await repository.UpdateAsync(client, request.TenantId, cancellationToken);

        return Result.Ok(new ClientSecretIssued(secret.Id, secret.Label, plainTextSecret, secret.ExpiresAt));
    }

    private static string GeneratePlainTextSecret() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
