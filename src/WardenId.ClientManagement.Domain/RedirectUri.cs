using FluentResults;
using WardenId.SharedKernel.Domain;

namespace WardenId.ClientManagement.Domain;

public sealed class RedirectUri : ValueObject
{
    public string Value { get; }

    private RedirectUri(string value) => Value = value;

    public static Result<RedirectUri> Create(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Result.Fail(ClientErrors.RedirectUriEmpty());

        var candidate = raw.Trim();

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
            return Result.Fail(ClientErrors.RedirectUriMustBeAbsolute());

        if (!string.IsNullOrEmpty(uri.Fragment))
            return Result.Fail(ClientErrors.RedirectUriCannotContainFragment());

        if (candidate.Contains('*'))
            return Result.Fail(ClientErrors.RedirectUriCannotContainWildcard());

        return Result.Ok(new RedirectUri(candidate));
    }

    public static RedirectUri FromTrustedSource(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Redirect uri cannot be null or empty.", nameof(value))
            : new RedirectUri(value);
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
