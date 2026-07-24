using FluentResults;
using WardenId.SharedKernel.Domain;

namespace WardenId.ClientManagement.Domain;

public sealed class CorsOrigin : ValueObject
{
    public string Value { get; }

    private CorsOrigin(string value) => Value = value;

    public static Result<CorsOrigin> Create(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Result.Fail(ClientErrors.CorsOriginEmpty());

        var candidate = raw.Trim();

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
            return Result.Fail(ClientErrors.CorsOriginInvalidFormat());

        if (uri.PathAndQuery != "/" || !string.IsNullOrEmpty(uri.Fragment))
            return Result.Fail(ClientErrors.CorsOriginCannotContainPath());

        return Result.Ok(new CorsOrigin(candidate));
    }

    public static CorsOrigin FromTrustedSource(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Cors origin cannot be null or empty.", nameof(value))
            : new CorsOrigin(value);
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
