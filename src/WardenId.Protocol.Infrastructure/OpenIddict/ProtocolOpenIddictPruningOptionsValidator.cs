using Microsoft.Extensions.Options;
using WardenId.Protocol.Application;

namespace WardenId.Protocol.Infrastructure.OpenIddict;

/// <summary>Fails boot on a pruning configuration that would silently break refresh-token reuse detection.</summary>
public sealed class ProtocolOpenIddictPruningOptionsValidator(IOptions<ProtocolTokenOptions> tokenOptions)
    : IValidateOptions<ProtocolOpenIddictPruningOptions>
{
    public ValidateOptionsResult Validate(string? name, ProtocolOpenIddictPruningOptions options)
    {
        var failures = new List<string>();

        if (options.PruneInterval <= TimeSpan.Zero)
            failures.Add($"{nameof(options.PruneInterval)} must be greater than zero.");

        var refreshLifetime = tokenOptions.Value.RefreshTokenAbsoluteLifetime;
        if (options.PruneMinimumAge < refreshLifetime)
            failures.Add(
                $"{nameof(options.PruneMinimumAge)} ({options.PruneMinimumAge}) must be at least the refresh token " +
                $"absolute lifetime ({refreshLifetime}): pruning a redeemed refresh token earlier would break reuse detection.");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
