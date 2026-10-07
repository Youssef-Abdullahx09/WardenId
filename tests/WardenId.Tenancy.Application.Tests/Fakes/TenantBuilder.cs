namespace WardenId.Tenancy.Application.Tests.Fakes;

public static class TenantBuilder
{
    public static string Jwk(Guid keyId) =>
        $$"""{"kty":"EC","crv":"P-256","x":"x-{{keyId:N}}","y":"y","alg":"ES256","use":"sig","kid":"{{keyId:N}}"}""";

    /// <summary>Creates a tenant with one ES256 key, transitioned to the requested status.</summary>
    public static (Tenant Tenant, SigningKey Key) Create(string slug, Status status)
    {
        var tenant = Tenant.Create(Slug.Create(slug).Value, "Display " + slug).Value;
        var keyId = Guid.NewGuid();
        var key = SigningKey.CreateEs256(keyId, Jwk(keyId), [1, 2, 3]);
        Assert.True(tenant.AssignInitialSigningKey(key).IsSuccess);

        switch (status)
        {
            case Status.Provisioning:
                break;
            case Status.Active:
                Assert.True(tenant.Activate().IsSuccess);
                break;
            case Status.Suspended:
                Assert.True(tenant.Activate().IsSuccess);
                Assert.True(tenant.Suspend("test").IsSuccess);
                break;
            case Status.Archived:
                Assert.True(tenant.Activate().IsSuccess);
                Assert.True(tenant.Archive().IsSuccess);
                break;
        }

        return (tenant, key);
    }
}
