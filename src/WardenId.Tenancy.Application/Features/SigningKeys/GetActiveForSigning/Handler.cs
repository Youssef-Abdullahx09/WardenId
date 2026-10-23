using FluentResults;
using MediatR;
using WardenId.Tenancy.Domain;
using WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetActiveForSigning;
using WardenId.Tenancy.Query.Contracts;
using SigningKeysGetActiveQuery = WardenId.Tenancy.Query.Contracts.Features.SigningKeys.GetActiveForSigning.Query;

namespace WardenId.Tenancy.Application.Features.SigningKeys.GetActiveForSigning;

// Inside this namespace, a bare \"Query\" would otherwise bind to the WardenId.Tenancy.Query namespace.
using Query = WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetActiveForSigning.Query;

/// <summary>
/// Read side via the Dapper query layer (Tenancy.Query). The private key is passed on still encrypted
/// (a plain bytea column, no EF converter); only Protocol decrypts it, at signing time.
/// </summary>
public sealed class Handler(ISigningKeysQuery signingKeysQuery) : IRequestHandler<Query, Result<ActiveSigningKey>>
{
    public async Task<Result<ActiveSigningKey>> Handle(Query request, CancellationToken cancellationToken)
    {
        var result = await signingKeysQuery.GetActiveForSigning(
            new SigningKeysGetActiveQuery(request.TenantId, nameof(SigningKeyStatus.Active)),
            cancellationToken);
        if (result.IsFailed) return Result.Fail(result.Errors);

        if (result.Value is not { } row) return Result.Fail(TenantErrors.NotFound);

        if (row is not { SigningKeyId: { } keyId, Algorithm: { } algorithm, EncryptedPrivateKey: { } encryptedPrivateKey })
            return Result.Fail(TenantErrors.NoActiveSigningKeyForSigning());

        return Result.Ok(new ActiveSigningKey(keyId, algorithm, encryptedPrivateKey, row.Slug));
    }
}
