using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.RequestPasswordReset;

public sealed class Handler(
    IUserRepository repository,
    IIdentityEmailSender emailSender) : IRequestHandler<Command, Result<PasswordResetRequested?>>
{
    private readonly PasswordResetIssuer _issuer = new(repository, emailSender);

    public async Task<Result<PasswordResetRequested?>> Handle(Command request, CancellationToken cancellationToken)
    {
        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailed) return Result.Ok<PasswordResetRequested?>(null);

        var userResult = await repository.GetByEmailAsync(request.TenantId, emailResult.Value, cancellationToken);
        if (userResult.IsFailed) return Result.Ok<PasswordResetRequested?>(null);

        var user = userResult.Value;

        // Disabled/soft-deleted targets are treated exactly like "no such user" — minting a token or
        // returning a distinguishing failure would leak account state to an anonymous caller.
        if (user.Status is UserStatus.Disabled or UserStatus.SoftDeleted)
            return Result.Ok<PasswordResetRequested?>(null);

        // A concurrent change to the user is answered like "no such user" too: a distinguishing failure would leak
        // that the account exists. Nothing was saved or sent; the user can ask again.
        var issueResult = await _issuer.IssueAsync(user, user.Id, cancellationToken);
        if (issueResult.IsFailed) return Result.Ok<PasswordResetRequested?>(null);

        return Result.Ok<PasswordResetRequested?>(new PasswordResetRequested(issueResult.Value));
    }
}
