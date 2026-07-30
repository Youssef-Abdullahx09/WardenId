using FluentResults;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application;

internal static class PasswordPolicyFactory
{
    public static Result<PasswordPolicy> Create(IdentityPolicyOptions options) =>
        PasswordPolicy.Create(
            options.PasswordMinLength,
            options.PasswordRequireUppercase,
            options.PasswordRequireDigit,
            options.PasswordRequireSymbol,
            options.PasswordBreachCheckEnabled,
            options.PasswordHistoryCount);
}
