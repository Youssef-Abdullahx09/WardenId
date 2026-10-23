using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.Protocol.Application.Features.Grants.RevokeForUser;
using WardenId.Protocol.Presentation.Utilities;
using WardenId.SharedKernel.Presentation;
using WardenId.SharedKernel.Presentation.Utilities;

namespace WardenId.Protocol.Presentation.Features.Grants.RevokeForUser;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet(ApiVersionSets.Grants)
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapDelete(ProtocolRoutes.Admin.UserGrants, async (
                [FromRoute] Guid tenantId,
                [FromRoute] Guid userId,
                [FromServices] ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new Command(tenantId, userId), cancellationToken);

                return result.IsSuccess ? Results.NoContent() : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.Grants.RevokeForUser)
            .RequireAuthorization()
            .WithTags(EndpointTags.Grants)
            .WithSummary("Revoke all of a User's Grants")
            .WithDescription(
                "Revokes every valid grant of the user in the tenant, offline_access ones included, with all their tokens. " +
                "Login sessions are not ended (use DELETE .../users/{userId}/sessions). Idempotent: 204 even when there is none.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
