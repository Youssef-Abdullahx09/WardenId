using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.Identity.Domain;
using WardenId.SharedKernel.Presentation;

namespace WardenId.Identity.Presentation.Features.Users.List;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet("Users")
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapGet("/api/v{version:apiVersion}/admin/tenants/{tenantId:guid}/users", async (
                [FromRoute] Guid tenantId,
                [FromQuery] int pageNumber,
                [FromQuery] int pageSize,
                [FromQuery] UserStatus? status,
                [FromServices] ISender sender,
                CancellationToken cancellationToken) =>
            {
                var request = new Request(pageNumber, pageSize, status);
                var result = await sender.Send(request.ToQuery(tenantId), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .RequireAuthorization()
            .WithTags("Identity - Users")
            .WithSummary("List users")
            .WithDescription("Lists users for the given tenant, paginated and optionally filtered by status.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
