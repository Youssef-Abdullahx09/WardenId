using WardenId.Tenancy.Application.Features.SigningKeys.GetPublicKeys;
using WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetPublicKeys;
using KeyRow = WardenId.Tenancy.Query.Contracts.Features.SigningKeys.GetPublicKeys.PublicKeyItem;
using SigningKeysGetPublicKeysQuery = WardenId.Tenancy.Query.Contracts.Features.SigningKeys.GetPublicKeys.Query;
using TenantRow = WardenId.Tenancy.Query.Contracts.Features.SigningKeys.GetPublicKeys.Item;

namespace WardenId.Tenancy.Application.Tests.Features.SigningKeys.GetPublicKeys;

// A bare \"Query\" would otherwise bind to the WardenId.Tenancy.Query namespace.
using Query = WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetPublicKeys.Query;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static FakeSigningKeysQuery QueryReturning(string status, params (SigningKeyStatus Status, string Jwk)[] keys) => new()
    {
        GetPublicKeysResponse = Result.Ok<TenantRow?>(new TenantRow
        {
            Id = TenantId,
            Slug = "acme",
            Status = status,
            PublicKeys = keys.Select(k => new KeyRow { Status = k.Status.ToString(), PublicKeyJwk = k.Jwk }).ToList()
        })
    };

    private static Task<Result<TenantPublicKeys>> HandleAsync(FakeSigningKeysQuery query, string slug = "acme") =>
        new Handler(query).Handle(new Query(slug), CancellationToken.None);

    [Theory]
    [InlineData(Status.Active)]
    [InlineData(Status.Suspended)]
    public async Task Handle_ReturnsKeys_WhenTenantIsServable(Status status)
    {
        var jwk = TenantBuilder.Jwk(Guid.NewGuid());
        var query = QueryReturning(status.ToString(), (SigningKeyStatus.Active, jwk));

        var result = await HandleAsync(query);

        Assert.True(result.IsSuccess);
        Assert.Equal(TenantId, result.Value.TenantId);
        Assert.Equal("acme", result.Value.Slug);
        Assert.Equal(new[] { jwk }, result.Value.PublicKeyJwks);
        Assert.Equal(new SigningKeysGetPublicKeysQuery("acme"), Assert.Single(query.GetPublicKeysCalls));
    }

    [Theory]
    [InlineData(nameof(Status.Provisioning))]
    [InlineData(nameof(Status.Archived))]
    [InlineData("SomeFutureStatus")]
    public async Task Handle_ReturnsNotFound_WhenTenantStatusIsNotServable(string status)
    {
        var query = QueryReturning(status, (SigningKeyStatus.Active, TenantBuilder.Jwk(Guid.NewGuid())));

        AssertNotFound(await HandleAsync(query));
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_WhenSlugIsUnknown() =>
        AssertNotFound(await HandleAsync(new FakeSigningKeysQuery(), "other"));

    [Fact]
    public async Task Handle_PublishesTheActiveAndRetiringKeys_ButNotRetiredOnes()
    {
        var retired = TenantBuilder.Jwk(Guid.NewGuid());
        var retiring = TenantBuilder.Jwk(Guid.NewGuid());
        var active = TenantBuilder.Jwk(Guid.NewGuid());
        var query = QueryReturning(nameof(Status.Active),
            (SigningKeyStatus.Active, active),
            (SigningKeyStatus.Retiring, retiring),
            (SigningKeyStatus.Retired, retired));

        var result = await HandleAsync(query);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { active, retiring }, result.Value.PublicKeyJwks);
    }

    private static void AssertNotFound(Result<TenantPublicKeys> result)
    {
        Assert.True(result.IsFailed);
        Assert.Equal(TenantErrorCodes.NotFound, result.Errors[0].Metadata[TenantErrorCodes.MetadataKey]);
    }
}
