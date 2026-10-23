using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.ClientManagement.Presentation.Utilities;
using WardenId.SharedKernel.Presentation;
using WardenId.SharedKernel.Presentation.Utilities;

namespace WardenId.ClientManagement.Presentation.Features.Clients.RedirectUris.Replace;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet(ApiVersionSets.Clients)
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapPut(
                ClientManagementRoutes.RedirectUris, async (
                [FromRoute] Guid tenantId,
                [FromRoute] Guid clientId,
                [FromBody] Request request,
                [FromServices] ISender sender,
                [FromServices] ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(request.ToCommand(tenantId, clientId, currentUser.UserId), cancellationToken);

                return result.IsSuccess ? Results.NoContent() : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.Clients.ReplaceRedirectUris)
            .RequireAuthorization()
            .WithTags(EndpointTags.Clients)
            .WithSummary("Replace redirect URIs")
            .WithDescription("Replaces the client's entire redirect URI set with the given collection (exact-match only, deduped). An empty collection clears it. One invalid entry rejects the whole request.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
