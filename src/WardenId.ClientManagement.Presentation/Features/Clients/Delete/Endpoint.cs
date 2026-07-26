using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.ClientManagement.Application.Features.Clients.Delete;
using WardenId.SharedKernel.Presentation;

namespace WardenId.ClientManagement.Presentation.Features.Clients.Delete;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet("Clients")
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapDelete("/api/v{version:apiVersion}/admin/tenants/{tenantId:guid}/clients/{clientId:guid}", async (
                [FromRoute] Guid tenantId,
                [FromRoute] Guid clientId,
                [FromServices] ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new Command(tenantId, clientId), cancellationToken);

                return result.IsSuccess ? Results.NoContent() : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .RequireAuthorization()
            .WithTags("ClientManagement - Clients")
            .WithSummary("Delete a Client")
            .WithDescription("Soft-deletes a client.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
