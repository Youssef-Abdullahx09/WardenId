using FluentResults;
using MediatR;
using IdentityGetAccountQuery = WardenId.Identity.Integration.Command.Features.Users.GetAccount.Query;

namespace WardenId.Protocol.Application.Features.Account.Get;

public sealed class Handler(ISender sender) : IRequestHandler<Query, Result<AccountSummary>>
{
    public async Task<Result<AccountSummary>> Handle(Query request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new IdentityGetAccountQuery(request.TenantId, request.UserId), cancellationToken);
        if (result.IsFailed) return Result.Fail(result.Errors);

        var account = result.Value;
        return Result.Ok(new AccountSummary(
            account.Email,
            account.Username,
            WebLink(account.TermsUrl),
            account.MfaAvailable,
            account.MfaRequired,
            account.Factors.Select(f => new AccountFactorSummary(f.Type, f.EnrolledAtUtc, f.LastUsedAtUtc)).ToArray(),
            account.RemainingRecoveryCodes));
    }

    /// <summary>Identity validates the link; checked again because it ends up in an href (no javascript: links).</summary>
    private static string? WebLink(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? uri.AbsoluteUri
            : null;
}
