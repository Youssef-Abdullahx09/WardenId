using FluentResults;

namespace WardenId.ClientManagement.Domain;

public static class ClientErrors
{
    public static Error NotFound =>
        new Error("Client not found.")
            .WithMetadata(ClientErrorCodes.MetadataKey, ClientErrorCodes.NotFound);

    // One error for every verification failure so callers can't tell which check failed.
    public static Error InvalidSecret() =>
        new Error("Invalid client credentials.")
            .WithMetadata(ClientErrorCodes.MetadataKey, ClientErrorCodes.InvalidCredentials);

    public static Error Validation(string reason)
        => new Error(reason)
            .WithMetadata(ClientErrorCodes.MetadataKey, ClientErrorCodes.Validation);

    public static Error RedirectUriEmpty()
        => Validation("Redirect URI is required.");

    public static Error RedirectUriMustBeAbsolute()
        => Validation("Redirect URI must be an absolute URI.");

    public static Error RedirectUriCannotContainFragment()
        => Validation("Redirect URI cannot contain a fragment.");

    public static Error RedirectUriCannotContainWildcard()
        => Validation("Redirect URI cannot contain a wildcard.");

    public static Error CorsOriginEmpty()
        => Validation("CORS origin is required.");

    public static Error CorsOriginInvalidFormat()
        => Validation("CORS origin must be a valid origin (scheme and host only).");

    public static Error CorsOriginCannotContainPath()
        => Validation("CORS origin cannot contain a path, query, or fragment.");

    public static Error TokenLifetimeMustBePositive(string fieldName)
        => Validation($"{fieldName} must be a positive number of seconds.");

    public static Error ClientDisplayNameRequired()
        => Validation("Client display name is required.");

    public static Error ClientDisplayNameTooLong()
        => Validation("Client display name cannot exceed 200 characters.");

    public static Error PublicClientCannotHaveSecret()
        => Validation("Public clients cannot have client secrets.");

    public static Error SecretNotFound()
        => Validation("Client secret not found.");

    public static Error SecretLabelRequired()
        => Validation("Client secret label is required.");

    public static Error SigningKeyRequiresPrivateKeyJwt()
        => Validation("Signing keys can only be added to clients using the PrivateKeyJwt authentication method.");

    public static Error BackchannelLogoutUriInvalid()
        => Validation("Back-channel logout URI must be an absolute URI without a fragment.");

    public static Error BackchannelLogoutUriMustBeHttps()
        => Validation("Back-channel logout URI must use https (http is allowed only for loopback hosts).");
}
