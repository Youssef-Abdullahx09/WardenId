namespace WardenId.Tenancy.Application.Tests.Fakes;

/// <summary>Returns a JWK whose kid is the requested key id; no real key material.</summary>
public sealed class FakeSigningKeyGenerator : ISigningKeyGenerator
{
    public List<Guid> GeneratedKeyIds { get; } = [];

    public SigningKeyMaterial GenerateEs256Async(Guid keyId)
    {
        GeneratedKeyIds.Add(keyId);
        return new SigningKeyMaterial(TenantBuilder.Jwk(keyId), [4, 5, 6]);
    }
}
