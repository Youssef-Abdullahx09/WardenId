using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.ClientManagement.Application.Features.Clients.Secrets.Issue;
using WardenId.ClientManagement.Presentation.Utilities;
using WardenId.SharedKernel.Presentation;
using WardenId.SharedKernel.Presentation.Utilities;

namespace WardenId.ClientManagement.Presentation.Features.Clients.Secrets.Issue;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet(ApiVersionSets.Clients)
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapPost(ClientManagementRoutes.Secrets, async (
                [FromRoute] Guid tenantId,
                [FromRoute] Guid clientId,
                [FromBody] Request request,
                [FromServices] ISender sender,
                [FromServices] ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(request.ToCommand(tenantId, clientId, currentUser.UserId), cancellationToken);

                return result.IsSuccess ? Results.Created(
                    ClientManagementRoutes.ClientLocation(tenantId, clientId), result.Value) : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.Clients.IssueSecret)
            .RequireAuthorization()
            .WithTags(EndpointTags.Clients)
            .WithSummary("Issue a Client secret")
            .WithDescription("Issues a new client secret, rotating out the prior active secret with an overlap window if one exists. The plaintext secret is only ever returned here, once.")
            .ProducesValidationProblem()
            .Produces<ClientSecretIssued>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
