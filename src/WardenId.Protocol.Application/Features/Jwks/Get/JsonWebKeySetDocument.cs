using System.Text.Json;
using System.Text.Json.Serialization;

namespace WardenId.Protocol.Application.Features.Jwks.Get;

/// <summary>RFC 7517 JWK Set. Keys are the stored public JWKs, served verbatim.</summary>
public sealed record JsonWebKeySetDocument(
    [property: JsonPropertyName("keys")] IReadOnlyList<JsonElement> Keys);
