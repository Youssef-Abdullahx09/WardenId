using FluentResults;
using MediatR;
using WardenId.Audit.Integration.Command.Features.Events.Record;
using WardenId.Protocol.Application.Auditing;
using WardenId.Protocol.Domain;
using IdentityAcceptTermsCommand = WardenId.Identity.Integration.Command.Features.Users.AcceptTerms.Command;

namespace WardenId.Protocol.Application.Features.Authorize.AcceptTerms;

public sealed class Handler(ISender sender, IAuthAuditTrail audit, TimeProvider timeProvider) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new IdentityAcceptTermsCommand(request.TenantId, request.UserId), cancellationToken);
        if (result.IsFailed)
            return Result.Fail(ProtocolErrors.Validation("We couldn't record that you accepted the terms. Please try again."));

        await audit.RecordAsync(UserChangeAudit.Changed(
            SecurityEvent.TermsAccepted, request.TenantId, request.UserId, timeProvider.GetUtcNow()));
        return Result.Ok();
    }
}
