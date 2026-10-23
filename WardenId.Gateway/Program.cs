using Swashbuckle.AspNetCore.SwaggerUI;
using WardenId.Audit.Presentation;
using WardenId.Gateway.Bootstrap;
using WardenId.Gateway.DependencyInjection;
using WardenId.Gateway.Health;
using WardenId.Gateway.Utilities;
using WardenId.SharedKernel.Infrastructure;
using WardenId.SharedKernel.Presentation;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddOpenTelemetryLogging(builder.Configuration);

builder.Services.AddSharedKernelModule(builder.Configuration);
builder.Services.AddAuditModule(builder.Configuration);
builder.Services.AddTenancyModule(builder.Configuration);
builder.Services.AddClientManagementModule(builder.Configuration);
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddProtocolModule(builder.Configuration);
builder.Services.AddGateway(builder.Configuration);
builder.Services.AddGatewayHealthChecks(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors();
// WardenId access tokens are the default scheme (AdminApiAuthentication), so context.User is the caller
// on REST endpoints. OpenIddict's server and the per-tenant session cookie (WardenId.TenantSession)
// are named explicitly by the protocol endpoints and login pages.
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Before endpoints so every handler runs with ambient audit context set — see
// docs/audit/PLAN.md §3.4. context.User is the access token's caller when one was sent, otherwise
// the unauthenticated principal. REST endpoints only require a valid access token; there is no
// authorization model (who may do what) on top.
app.UseMiddleware<AuditContextMiddleware>();

app.MapEndpoints();

// /health/live and /health/ready: anonymous, outside /api and Swagger (WardenId.Gateway/Health).
app.MapGatewayHealthChecks();

// Protocol login pages (Razor class library in WardenId.Protocol.Presentation). Their stylesheet and
// script are the RCL's wwwroot, served anonymously at /_content/WardenId.Protocol.Presentation/...
app.MapStaticAssets();
app.MapRazorPages();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(SwaggerDocument.JsonEndpoint, SwaggerDocument.DisplayName);
        options.RoutePrefix = SwaggerDocument.RoutePrefix;
        options.ConfigObject.PersistAuthorization = true; // Keep token after page refresh
        options.ConfigObject.DisplayRequestDuration = true; // Display request duration
        options.ConfigObject.TryItOutEnabled = true; // Enable Try It Out button
        options.ConfigObject.DocExpansion = DocExpansion.None; // Hide the default expansion of the sections
        options.ConfigObject.DefaultModelRendering = ModelRendering.Example; // Show the example value in the model section
    });
}

// Hosted services (including CAP's storage bootstrapper, which creates the "cap.published"
// outbox table) only start here — start the host explicitly so the seeders, which publish
// domain events through CAP, run after that table exists rather than racing it.
await app.StartAsync(CancellationToken.None);

await MasterTenantSeeder.SeedAsync(app);
await MasterAdminSeeder.SeedAsync(app);
await MasterClientSeeder.SeedAsync(app);

await app.WaitForShutdownAsync(CancellationToken.None);
