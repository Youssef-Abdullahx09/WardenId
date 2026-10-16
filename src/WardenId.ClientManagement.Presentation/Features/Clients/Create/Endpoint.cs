using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.SharedKernel.Presentation;

namespace WardenId.ClientManagement.Presentation.Features.Clients.Create;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet("Clients")
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapPost("/api/v{version:apiVersion}/admin/tenants/{tenantId:guid}/clients", async (
                [FromRoute] Guid tenantId,
                [FromBody] Request request,
                [FromServices] ISender sender,
                [FromServices] ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(request.ToCommand(tenantId, currentUser.UserId), cancellationToken);

                return result.IsSuccess
                    ? Results.Created($"/api/v1/admin/tenants/{tenantId}/clients/{result.Value}", new Response(result.Value))
                    : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.Clients.Create)
            .RequireAuthorization()
            .WithTags("ClientManagement - Clients")
            .WithSummary("Create a Client")
            .WithDescription("Creates an OAuth client for the given tenant.")
            .Produces<Response>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
