using System.Text.Json.Serialization;

namespace WardenId.Protocol.Application.Features.Discovery.Get;

/// <summary>
/// OIDC Discovery 1.0 metadata. Honest subset: only fields that are true today. backchannel_logout_*
/// advertise OIDC Back-Channel Logout; every logout_token carries sid. Grant types, response types, code challenge methods and scopes must equal what
/// OpenIddict has enabled; endpoint auth methods must equal OpenIddict's client authentication methods (+ "none"
/// where public clients are allowed). Pinned by a drift test.
/// </summary>
public sealed record DiscoveryDocument(
    [property: JsonPropertyName("issuer")] string Issuer,
    [property: JsonPropertyName("jwks_uri")] string JwksUri,
    [property: JsonPropertyName("response_types_supported")] IReadOnlyList<string> ResponseTypesSupported,
    [property: JsonPropertyName("subject_types_supported")] IReadOnlyList<string> SubjectTypesSupported,
    [property: JsonPropertyName("id_token_signing_alg_values_supported")] IReadOnlyList<string> IdTokenSigningAlgValuesSupported,
    [property: JsonPropertyName("token_endpoint")] string TokenEndpoint,
    [property: JsonPropertyName("grant_types_supported")] IReadOnlyList<string> GrantTypesSupported,
    [property: JsonPropertyName("token_endpoint_auth_methods_supported")] IReadOnlyList<string> TokenEndpointAuthMethodsSupported,
    [property: JsonPropertyName("authorization_endpoint")] string AuthorizationEndpoint,
    [property: JsonPropertyName("scopes_supported")] IReadOnlyList<string> ScopesSupported,
    [property: JsonPropertyName("code_challenge_methods_supported")] IReadOnlyList<string> CodeChallengeMethodsSupported,
    [property: JsonPropertyName("authorization_response_iss_parameter_supported")] bool AuthorizationResponseIssParameterSupported,
    [property: JsonPropertyName("introspection_endpoint")] string IntrospectionEndpoint,
    [property: JsonPropertyName("introspection_endpoint_auth_methods_supported")] IReadOnlyList<string> IntrospectionEndpointAuthMethodsSupported,
    [property: JsonPropertyName("revocation_endpoint")] string RevocationEndpoint,
    [property: JsonPropertyName("revocation_endpoint_auth_methods_supported")] IReadOnlyList<string> RevocationEndpointAuthMethodsSupported,
    [property: JsonPropertyName("userinfo_endpoint")] string UserInfoEndpoint,
    [property: JsonPropertyName("end_session_endpoint")] string EndSessionEndpoint,
    [property: JsonPropertyName("backchannel_logout_supported")] bool BackchannelLogoutSupported,
    [property: JsonPropertyName("backchannel_logout_session_supported")] bool BackchannelLogoutSessionSupported);
