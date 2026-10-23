using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.Protocol.Application.Features.Sessions.End;
using WardenId.Protocol.Domain.Sessions;
using WardenId.SharedKernel.Presentation;

namespace WardenId.Protocol.Presentation.Features.Sessions.End;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet(ApiVersionSets.Sessions)
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapDelete(ProtocolRoutes.Admin.Session, async (
                [FromRoute] Guid tenantId,
                [FromRoute] Guid sessionId,
                [FromServices] ISender sender,
                CancellationToken cancellationToken) =>
            {
                // RequireExisting: an operator ending a missing, foreign or already-ended session gets 404, not a silent 204.
                var result = await sender.Send(
                    new Command(tenantId, sessionId, EndReason.AdminEnded, RequireExisting: true), cancellationToken);

                return result.IsSuccess ? Results.NoContent() : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.Sessions.End)
            .RequireAuthorization()
            .WithTags(EndpointTags.Sessions)
            .WithSummary("End a Session")
            .WithDescription("Ends one live login session. Its non-offline refresh tokens are revoked and participating clients receive a back-channel logout.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
