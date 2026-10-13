using System.Text.Json;
using WardenId.ClientManagement.Integration.Command.Features.Clients.GetForAuthentication;
using WardenId.Protocol.Infrastructure.OpenIddict.Stores;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace WardenId.Protocol.Infrastructure.Tests.Stores;

public class ClientApplicationMapperTests
{
    private static ClientForAuthentication Client(
        ClientKind kind, AllowedGrant[] grants, bool requirePkce = false, params string[] redirectUris) =>
        new(Guid.NewGuid(), "web", kind, true, grants, ClientAuthMethod.SharedSecret, null, null, redirectUris, requirePkce);

    private static string[] Json(string? value) =>
        value is null ? [] : JsonSerializer.Deserialize<string[]>(value)!;

    [Fact]
    public void AuthorizationCodeClient_GetsCodeFlowPermissions_AndRedirectUris()
    {
        var app = ClientApplicationMapper.Map(Client(
            ClientKind.Confidential, [AllowedGrant.AuthorizationCode], false, "https://app.example/cb"));

        var permissions = Json(app.Permissions);
        Assert.Contains(Permissions.Endpoints.Authorization, permissions);
        Assert.Contains(Permissions.Endpoints.Token, permissions);
        Assert.Contains(Permissions.GrantTypes.AuthorizationCode, permissions);
        Assert.Contains(Permissions.ResponseTypes.Code, permissions);
        Assert.Contains(Permissions.Scopes.Profile, permissions);
        Assert.Contains(Permissions.Scopes.Email, permissions);
        Assert.DoesNotContain(Permissions.GrantTypes.ClientCredentials, permissions);
        Assert.Equal(new[] { "https://app.example/cb" }, Json(app.RedirectUris));
    }

    [Fact]
    public void ClientCredentialsOnlyClient_HasNoAuthorizationPermissions()
    {
        var app = ClientApplicationMapper.Map(Client(ClientKind.Confidential, [AllowedGrant.ClientCredentials]));

        var permissions = Json(app.Permissions);
        Assert.Equal(new[] { Permissions.Endpoints.Token, Permissions.GrantTypes.ClientCredentials }, permissions);
    }

    [Fact]
    public void PublicClient_RequiresPkce()
    {
        var app = ClientApplicationMapper.Map(Client(ClientKind.Public, [AllowedGrant.AuthorizationCode], requirePkce: false));

        Assert.Equal(new[] { Requirements.Features.ProofKeyForCodeExchange }, Json(app.Requirements));
        Assert.Equal(ClientTypes.Public, app.ClientType);
        Assert.Null(app.ClientSecret);
    }

    [Fact]
    public void ConfidentialClient_WithRequirePkce_RequiresPkce()
    {
        var app = ClientApplicationMapper.Map(Client(ClientKind.Confidential, [AllowedGrant.AuthorizationCode], requirePkce: true));

        Assert.Equal(new[] { Requirements.Features.ProofKeyForCodeExchange }, Json(app.Requirements));
    }

    [Fact]
    public void ConfidentialClient_WithoutRequirePkce_HasNoRequirements()
    {
        var app = ClientApplicationMapper.Map(Client(ClientKind.Confidential, [AllowedGrant.AuthorizationCode], requirePkce: false));

        Assert.Empty(Json(app.Requirements));
        Assert.Equal(ClientTypes.Confidential, app.ClientType);
    }

    [Fact]
    public void RefreshTokenGrant_AddsRefreshTokenPermission()
    {
        var app = ClientApplicationMapper.Map(Client(
            ClientKind.Confidential, [AllowedGrant.AuthorizationCode, AllowedGrant.RefreshToken], false, "https://app.example/cb"));

        Assert.Contains(Permissions.GrantTypes.RefreshToken, Json(app.Permissions));
    }

    [Fact]
    public void WithoutRefreshTokenGrant_HasNoRefreshTokenPermission()
    {
        var app = ClientApplicationMapper.Map(Client(
            ClientKind.Confidential, [AllowedGrant.AuthorizationCode], false, "https://app.example/cb"));

        Assert.DoesNotContain(Permissions.GrantTypes.RefreshToken, Json(app.Permissions));
    }

    // OpenIddict's authorize-time grant check (ValidateGrantTypePermissions, ID2047) is removed; the code flow is
    // then gated only by the authorization endpoint + code response type permissions. They must travel with the
    // authorization_code grant permission: all three or none.
    public static TheoryData<AllowedGrant[], bool> CodeFlowGrantSets => new()
    {
        { [AllowedGrant.AuthorizationCode], true },
        { [AllowedGrant.AuthorizationCode, AllowedGrant.RefreshToken], true },
        { [AllowedGrant.AuthorizationCode, AllowedGrant.ClientCredentials, AllowedGrant.RefreshToken], true },
        { [AllowedGrant.ClientCredentials], false },
        { [AllowedGrant.RefreshToken], false },
        { [AllowedGrant.ClientCredentials, AllowedGrant.RefreshToken], false }
    };

    [Theory]
    [MemberData(nameof(CodeFlowGrantSets))]
    public void CodeFlowPermissions_AreAllPresentOrAllAbsent(AllowedGrant[] grants, bool codeFlow)
    {
        var permissions = Json(ClientApplicationMapper.Map(Client(ClientKind.Confidential, grants)).Permissions);

        string[] codeFlowPermissions =
            [Permissions.Endpoints.Authorization, Permissions.ResponseTypes.Code, Permissions.GrantTypes.AuthorizationCode];
        foreach (var permission in codeFlowPermissions)
            Assert.Equal(codeFlow, permissions.Contains(permission));
    }
}
