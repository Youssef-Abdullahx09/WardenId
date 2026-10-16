using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.SharedKernel.Presentation;

namespace WardenId.Identity.Presentation.Features.Users.RequestPasswordReset;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet("Users")
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapPost("/api/v{version:apiVersion}/tenants/{tenantId:guid}/auth/password-reset/request", async (
                [FromRoute] Guid tenantId,
                [FromBody] Request request,
                [FromServices] ISender sender,
                CancellationToken cancellationToken) =>
            {
                // Always responds identically whether or not the email matches an account — the raw
                // reset token (when one was minted) never leaves this process via HTTP; it only ever
                // reaches the account owner through the reset email sent inside the handler.
                await sender.Send(request.ToCommand(tenantId), cancellationToken);

                return Results.Accepted();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.Users.RequestPasswordReset)
            .WithTags("Identity - Users")
            .WithSummary("Request a password reset")
            .WithDescription("Always returns 202 regardless of whether the email matches an account, to avoid user enumeration.")
            .Produces(StatusCodes.Status202Accepted);
    }
}
