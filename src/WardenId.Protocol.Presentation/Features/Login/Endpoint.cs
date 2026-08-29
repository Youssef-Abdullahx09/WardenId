using System.Text.Json;
using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.SharedKernel.FileOperation;
using WardenId.SharedKernel.Presentation;

namespace WardenId.Protocol.Presentation.Features.Login;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet("Protocol")
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = app.MapGroup("/api/v{version:apiVersion}/tenants/{tenantId:guid}/auth/login")
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithTags("Protocol - Login");

        group.MapGet("", async (
                [FromServices] IReadFileService readFileService,
                [FromServices] IAntiforgery antiforgery,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                httpContext.Response.Headers.CacheControl = "no-store";
                httpContext.Response.Headers.Pragma = "no-cache";

                var tokens = antiforgery.GetAndStoreTokens(httpContext);
                var html = await LoginPageRenderer.RenderFormAsync(
                    readFileService, errorMessage: "", tokens.FormFieldName, tokens.RequestToken ?? string.Empty, cancellationToken);
                return Results.Content(html, "text/html");
            })
            .WithSummary("Render the login page")
            .Produces(StatusCodes.Status200OK);

        group.MapPost("", async (
                [FromRoute] Guid tenantId,
                [FromForm] Request request,
                [FromServices] ISender sender,
                [FromServices] IReadFileService readFileService,
                [FromServices] IAntiforgery antiforgery,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                httpContext.Response.Headers.CacheControl = "no-store";
                httpContext.Response.Headers.Pragma = "no-cache";

                var result = await sender.Send(request.ToCommand(tenantId), cancellationToken);

                if (result.IsFailed)
                {
                    var tokens = antiforgery.GetAndStoreTokens(httpContext);
                    var errorHtml = await LoginPageRenderer.RenderFormAsync(
                        readFileService, result.Errors.FirstOrDefault()?.Message ?? "Sign in failed.",
                        tokens.FormFieldName, tokens.RequestToken ?? string.Empty, cancellationToken);
                    return Results.Content(errorHtml, "text/html", statusCode: StatusCodes.Status400BadRequest);
                }

                if (result.Value.RequiresAdditionalAction)
                {
                    var tokens = antiforgery.GetAndStoreTokens(httpContext);
                    var pendingHtml = await LoginPageRenderer.RenderFormAsync(
                        readFileService,
                        "Additional action required before you can sign in — verify your email or complete account setup.",
                        tokens.FormFieldName, tokens.RequestToken ?? string.Empty, cancellationToken);
                    return Results.Content(pendingHtml, "text/html");
                }

                var payloadJson = JsonSerializer.Serialize(result.Value.Tokens, new JsonSerializerOptions { WriteIndented = true });
                var successHtml = await LoginPageRenderer.RenderSuccessAsync(readFileService, payloadJson, cancellationToken);
                return Results.Content(successHtml, "text/html");
            })
            .WithSummary("Authenticate and issue tokens")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        return group;
    }
}
