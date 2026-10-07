using System.Text.Json.Serialization;

namespace WardenId.Protocol.Application.Features.Discovery.Get;

/// <summary>
/// OIDC Discovery 1.0 metadata. Honest subset: only fields that are true today. Later passes
/// append authorization_endpoint, token_endpoint, userinfo_endpoint, etc. as those ship.
/// </summary>
public sealed record DiscoveryDocument(
    [property: JsonPropertyName("issuer")] string Issuer,
    [property: JsonPropertyName("jwks_uri")] string JwksUri,
    [property: JsonPropertyName("response_types_supported")] IReadOnlyList<string> ResponseTypesSupported,
    [property: JsonPropertyName("subject_types_supported")] IReadOnlyList<string> SubjectTypesSupported,
    [property: JsonPropertyName("id_token_signing_alg_values_supported")] IReadOnlyList<string> IdTokenSigningAlgValuesSupported);
