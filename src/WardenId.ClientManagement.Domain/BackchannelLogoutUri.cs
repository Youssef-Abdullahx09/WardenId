using FluentResults;
using WardenId.SharedKernel.Domain;

namespace WardenId.ClientManagement.Domain;

/// <summary>
/// Where the OP POSTs the OIDC Back-Channel Logout <c>logout_token</c>. Server-to-server and carries a signed
/// session identifier, so TLS is mandatory; plain http is tolerated only for loopback hosts (local development).
/// </summary>
public sealed class BackchannelLogoutUri : ValueObject
{
    public string Value { get; }

    private BackchannelLogoutUri(string value) => Value = value;

    public static Result<BackchannelLogoutUri> Create(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Result.Fail(ClientErrors.BackchannelLogoutUriInvalid());

        var candidate = raw.Trim();

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) || !string.IsNullOrEmpty(uri.Fragment)
            || !string.IsNullOrEmpty(uri.UserInfo))
            return Result.Fail(ClientErrors.BackchannelLogoutUriInvalid());

        var secure = uri.Scheme == Uri.UriSchemeHttps
                     || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback);

        return secure
            ? Result.Ok(new BackchannelLogoutUri(candidate))
            : Result.Fail(ClientErrors.BackchannelLogoutUriMustBeHttps());
    }

    public static BackchannelLogoutUri FromTrustedSource(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Back-channel logout uri cannot be null or empty.", nameof(value))
            : new BackchannelLogoutUri(value);
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
