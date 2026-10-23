using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.Protocol.Application.Features.Sessions.EndForUser;
using WardenId.Protocol.Domain.Sessions;
using WardenId.Protocol.Presentation.Utilities;
using WardenId.SharedKernel.Presentation;
using WardenId.SharedKernel.Presentation.Utilities;

namespace WardenId.Protocol.Presentation.Features.Sessions.EndForUser;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet(ApiVersionSets.Sessions)
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapDelete(ProtocolRoutes.Admin.UserSessions, async (
                [FromRoute] Guid tenantId,
                [FromRoute] Guid userId,
                [FromServices] ISender sender,
                CancellationToken cancellationToken) =>
            {
                // Tenant-scoped, unlike the Identity-triggered EndForUser (TenantId null).
                var result = await sender.Send(new Command(userId, EndReason.AdminEnded, tenantId), cancellationToken);

                return result.IsSuccess ? Results.NoContent() : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.Sessions.EndForUser)
            .RequireAuthorization()
            .WithTags(EndpointTags.Sessions)
            .WithSummary("End all of a User's Sessions")
            .WithDescription("Ends every unended login session of the user in the tenant (expired-but-unended included). Idempotent: 204 even when there is none.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
