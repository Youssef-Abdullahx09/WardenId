using WardenId.Tenancy.Application.Features.SigningKeys.GetActiveForSigning;
using WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetActiveForSigning;
using KeyRow = WardenId.Tenancy.Query.Contracts.Features.SigningKeys.GetActiveForSigning.Item;
using SigningKeysGetActiveQuery = WardenId.Tenancy.Query.Contracts.Features.SigningKeys.GetActiveForSigning.Query;

namespace WardenId.Tenancy.Application.Tests.Features.SigningKeys.GetActiveForSigning;

// A bare \"Query\" would otherwise bind to the WardenId.Tenancy.Query namespace.
using Query = WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetActiveForSigning.Query;

public class HandlerTests
{
    [Fact]
    public async Task Handle_AsksForTheActiveKey_ByStatusName()
    {
        var query = new FakeSigningKeysQuery();
        var tenantId = Guid.NewGuid();

        await new Handler(query).Handle(new Query(tenantId), CancellationToken.None);

        Assert.Equal(
            new SigningKeysGetActiveQuery(tenantId, nameof(SigningKeyStatus.Active)),
            Assert.Single(query.GetActiveForSigningCalls));
    }

    [Fact]
    public async Task Handle_ReturnsSlugWithActiveKey_StillEncrypted()
    {
        var keyId = Guid.NewGuid();
        byte[] encrypted = [1, 2, 3];
        var query = new FakeSigningKeysQuery
        {
            GetActiveForSigningResponse = Result.Ok<KeyRow?>(new KeyRow
            {
                Slug = "acme", SigningKeyId = keyId, Algorithm = "ES256", EncryptedPrivateKey = encrypted
            })
        };

        var result = await new Handler(query).Handle(new Query(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(keyId, result.Value.KeyId);
        Assert.Equal("ES256", result.Value.Algorithm);
        Assert.Equal(encrypted, result.Value.EncryptedPrivateKey);
        Assert.Equal("acme", result.Value.Slug);
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_ForUnknownTenant()
    {
        var result = await new Handler(new FakeSigningKeysQuery()).Handle(new Query(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(TenantErrorCodes.NotFound, result.Errors[0].Metadata[TenantErrorCodes.MetadataKey]);
    }

    [Fact]
    public async Task Handle_Fails_WhenTheTenantHasNoActiveKey()
    {
        var query = new FakeSigningKeysQuery
        {
            GetActiveForSigningResponse = Result.Ok<KeyRow?>(new KeyRow { Slug = "acme" })
        };

        var result = await new Handler(query).Handle(new Query(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(
            TenantErrors.NoActiveSigningKeyForSigning().Metadata[TenantErrorCodes.MetadataKey],
            result.Errors[0].Metadata[TenantErrorCodes.MetadataKey]);
    }
}
