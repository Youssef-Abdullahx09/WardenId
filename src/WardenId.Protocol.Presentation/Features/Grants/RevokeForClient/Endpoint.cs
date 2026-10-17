using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.Protocol.Application.Features.Grants.RevokeForClient;
using WardenId.SharedKernel.Presentation;

namespace WardenId.Protocol.Presentation.Features.Grants.RevokeForClient;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet("Grants")
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapDelete("/api/v{version:apiVersion}/admin/tenants/{tenantId:guid}/clients/{clientId:guid}/grants", async (
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
            .WithName(EndpointNames.Grants.RevokeForClient)
            .RequireAuthorization()
            .WithTags("Protocol - Grants")
            .WithSummary("Revoke all of a Client's Grants and Tokens")
            .WithDescription(
                "clientId is the client's id (Guid), as in .../clients/{clientId}. Revokes every valid grant of the client " +
                "in the tenant, for every user and offline_access included, with all their refresh tokens, codes and " +
                "access tokens, and the client's client_credentials access tokens. Only rows issued since WardenId started " +
                "stamping tenant and client on them are found. Access tokens are JWTs: introspection reports them " +
                "inactive at once, but APIs that only check the signature (including WardenId's own admin API) accept " +
                "them until they expire. Login sessions are not ended. 204 even when there is nothing to revoke; 404 " +
                "when the client doesn't exist in the tenant or is deleted.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
