using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.Protocol.Application.Features.Grants;
using WardenId.SharedKernel.Presentation;
using ListForUserQuery = WardenId.Protocol.Application.Features.Grants.ListForUser.Query;

namespace WardenId.Protocol.Presentation.Features.Grants.ListForUser;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet("Grants")
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapGet("/api/v{version:apiVersion}/admin/tenants/{tenantId:guid}/users/{userId:guid}/grants", async (
                [FromRoute] Guid tenantId,
                [FromRoute] Guid userId,
                [FromServices] ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new ListForUserQuery(tenantId, userId), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.Grants.ListForUser)
            .RequireAuthorization()
            .WithTags("Protocol - Grants")
            .WithSummary("List a User's Grants")
            .WithDescription(
                "Lists the apps the user signed in to in the tenant: one OpenIddict authorization per /authorize sign-in, " +
                "newest first, with its client_id, scopes, whether it holds offline_access (refresh tokens that outlive the " +
                "login session) and its status. Revoked grants stay listed until pruned.")
            .Produces<IReadOnlyList<UserGrant>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
