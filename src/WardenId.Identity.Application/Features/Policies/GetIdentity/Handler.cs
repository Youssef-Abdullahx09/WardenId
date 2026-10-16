using System.Xml;
using FluentResults;
using MediatR;
using Microsoft.Extensions.Options;

namespace WardenId.Identity.Application.Features.Policies.GetIdentity;

public sealed class Handler(IOptions<IdentityPolicyOptions> options) : IRequestHandler<Query, Result<IdentityPolicy>>
{
    public const string GlobalScope = "global";

    public Task<Result<IdentityPolicy>> Handle(Query request, CancellationToken cancellationToken)
    {
        var policy = options.Value;

        var result = new IdentityPolicy(
            new PasswordSettings(
                policy.PasswordMinLength,
                policy.PasswordRequireUppercase,
                policy.PasswordRequireDigit,
                policy.PasswordRequireSymbol,
                policy.PasswordBreachCheckEnabled,
                policy.PasswordHistoryCount),
            new LockoutSettings(policy.MaxFailedLoginAttempts, XmlConvert.ToString(policy.LockoutDuration)),
            new MfaSettings(policy.MfaPolicyMode),
            GlobalScope);

        return Task.FromResult(Result.Ok(result));
    }
}
