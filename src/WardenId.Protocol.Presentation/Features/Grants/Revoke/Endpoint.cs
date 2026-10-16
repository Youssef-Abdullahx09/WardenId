using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.Protocol.Application.Features.Grants.Revoke;
using WardenId.SharedKernel.Presentation;

namespace WardenId.Protocol.Presentation.Features.Grants.Revoke;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet("Grants")
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapDelete("/api/v{version:apiVersion}/admin/tenants/{tenantId:guid}/users/{userId:guid}/grants/{authorizationId}", async (
                [FromRoute] Guid tenantId,
                [FromRoute] Guid userId,
                [FromRoute] string authorizationId,
                [FromServices] ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new Command(tenantId, userId, authorizationId), cancellationToken);

                return result.IsSuccess ? Results.NoContent() : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.Grants.Revoke)
            .RequireAuthorization()
            .WithTags("Protocol - Grants")
            .WithSummary("Revoke a User's Grant")
            .WithDescription(
                "Revokes one authorization and every token issued under it (refresh, access and unredeemed codes). " +
                "Already revoked: 204 again. Unknown, or another user's or tenant's: 404. The login session is not ended.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
