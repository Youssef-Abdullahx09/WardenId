using Microsoft.Extensions.Options;
using WardenId.Protocol.Application;

namespace WardenId.Protocol.Infrastructure.Sessions;

/// <summary>Fails boot on a session configuration that would prune rows of sessions that can still be live.</summary>
public sealed class ProtocolSessionOptionsValidator : IValidateOptions<ProtocolSessionOptions>
{
    public ValidateOptionsResult Validate(string? name, ProtocolSessionOptions options)
    {
        var failures = new List<string>();

        if (options.AbsoluteLifetime <= TimeSpan.Zero)
            failures.Add($"{nameof(options.AbsoluteLifetime)} must be greater than zero.");

        if (options.PruneMinimumAge < options.AbsoluteLifetime)
            failures.Add(
                $"{nameof(options.PruneMinimumAge)} ({options.PruneMinimumAge}) must be at least " +
                $"{nameof(options.AbsoluteLifetime)} ({options.AbsoluteLifetime}).");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
