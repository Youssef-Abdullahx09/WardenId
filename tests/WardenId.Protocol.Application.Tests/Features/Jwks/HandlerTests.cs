using System.Text.Json;
using WardenId.Protocol.Application.Features.Jwks.Get;
using WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetPublicKeys;
using Query = WardenId.Protocol.Application.Features.Jwks.Get.Query;

namespace WardenId.Protocol.Application.Tests.Features.Jwks;

public class HandlerTests
{
    private const string Jwk1 = """{"kty":"EC","crv":"P-256","x":"a","y":"b","alg":"ES256","use":"sig","kid":"k1"}""";
    private const string Jwk2 = """{"kty":"EC","crv":"P-256","x":"c","y":"d","alg":"ES256","use":"sig","kid":"k2"}""";

    private static FakeSender SenderWith(params string[] jwks) =>
        new() { PublicKeysResponse = Result.Ok(new TenantPublicKeys(Guid.NewGuid(), "acme", jwks)) };

    [Fact]
    public async Task Handle_ReturnsStoredJwksVerbatim()
    {
        var result = await new Handler(SenderWith(Jwk1, Jwk2)).Handle(new Query("acme"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            $$"""{"keys":[{{Jwk1}},{{Jwk2}}]}""",
            JsonSerializer.Serialize(result.Value));
    }

    [Fact]
    public async Task Handle_ReturnsEmptySet_WhenTenantHasNoKeys()
    {
        var result = await new Handler(SenderWith()).Handle(new Query("acme"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Keys);
    }

    [Fact]
    public async Task Handle_PropagatesNotFound()
    {
        var notFound = new Error("Tenant not found.").WithMetadata("ErrorCode", "NotFound");
        var sender = new FakeSender { PublicKeysResponse = Result.Fail<TenantPublicKeys>(notFound) };

        var result = await new Handler(sender).Handle(new Query("acme"), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal("NotFound", result.Errors[0].Metadata["ErrorCode"]);
    }

    [Fact]
    public async Task Handle_Throws_WhenStoredJwkIsNotValidJson()
    {
        var handler = new Handler(SenderWith(Jwk1, "{not json"));

        await Assert.ThrowsAnyAsync<JsonException>(() => handler.Handle(new Query("acme"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_Throws_WhenStoredJwkContainsPrivateParameter()
    {
        const string privateJwk = """{"kty":"EC","crv":"P-256","x":"a","y":"b","d":"secret","kid":"k1"}""";
        var handler = new Handler(SenderWith(privateJwk));

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new Query("acme"), CancellationToken.None));
    }
}
