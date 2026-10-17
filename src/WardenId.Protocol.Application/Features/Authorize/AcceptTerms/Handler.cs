using FluentResults;
using MediatR;
using WardenId.Protocol.Domain;
using IdentityAcceptTermsCommand = WardenId.Identity.Integration.Command.Features.Users.AcceptTerms.Command;

namespace WardenId.Protocol.Application.Features.Authorize.AcceptTerms;

public sealed class Handler(ISender sender) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new IdentityAcceptTermsCommand(request.TenantId, request.UserId), cancellationToken);
        return result.IsSuccess
            ? Result.Ok()
            : Result.Fail(ProtocolErrors.Validation("We couldn't record that you accepted the terms. Please try again."));
    }
}
