using WardenId.Protocol.Application.Utilities;

namespace WardenId.Protocol.Application.Tests;

public class IssuerUriTests
{
    [Theory]
    [InlineData("https://wardenid.test", "acme", "https://wardenid.test/tenants/acme")]
    [InlineData("https://wardenid.test/", "acme", "https://wardenid.test/tenants/acme")]
    [InlineData("https://host/base", "my-co", "https://host/base/tenants/my-co")]
    public void For_BuildsTenantIssuer(string baseUri, string slug, string expected) =>
        Assert.Equal(expected, IssuerUri.For(baseUri, slug));
}
