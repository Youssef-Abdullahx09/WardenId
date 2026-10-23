using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.SharedKernel.Presentation;
using WardenId.SharedKernel.Presentation.Utilities;
using WardenId.Tenancy.Presentation.Utilities;

namespace WardenId.Tenancy.Presentation.Features.Tenants.Create;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet(ApiVersionSets.Tenants)
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapPost(TenancyRoutes.Tenants.Collection, async (
                [FromBody] Request request,
                [FromServices] ISender sender,
                [FromServices] ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(request.ToCommand(currentUser.UserId), cancellationToken);

                return result.IsSuccess
                    ? Results.Created(TenancyRoutes.Tenants.Location(result.Value), new Response(result.Value))
                    : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.Tenants.Create)
            .RequireAuthorization()
            .WithTags(EndpointTags.Tenants)
            .WithSummary("Create a Tenant")
            .WithDescription("Creates a Tenant with the given name. Slug are unique (case-insensitive).")
            .Produces<Response>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
