using Swashbuckle.AspNetCore.SwaggerUI;
using WardenId.Audit.Presentation;
using WardenId.Gateway.Bootstrap;
using WardenId.Gateway.DependencyInjection;
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

var app = builder.Build();

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors();
app.UseAntiforgery();

// Before endpoints so every handler runs with ambient audit context set — see
// docs/audit/PLAN.md §3.4. No UseAuthentication()/UseAuthorization() exists yet anywhere
// in the app (RBAC/JWT auth is greenfield), so context.User is the framework's default,
// unauthenticated ClaimsPrincipal for every request until that lands.
app.UseMiddleware<AuditContextMiddleware>();

app.MapEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "WardenId v1.0");
        options.RoutePrefix = "swagger"; // Swagger will be at /swagger
        options.ConfigObject.PersistAuthorization = true; // Keep token after page refresh
        options.ConfigObject.DisplayRequestDuration = true; // Display request duration
        options.ConfigObject.TryItOutEnabled = true; // Enable Try It Out button
        options.ConfigObject.DocExpansion = DocExpansion.None; // Hide the default expansion of the sections
        options.ConfigObject.DefaultModelRendering = ModelRendering.Example; // Show the example value in the model section
    });
}

// Hosted services (including CAP's storage bootstrapper, which creates the "cap.published"
// outbox table) only start here — start the host explicitly so both seeders, which publish
// domain events through CAP, run after that table exists rather than racing it.
await app.StartAsync(CancellationToken.None);

await MasterTenantSeeder.SeedAsync(app);
await MasterAdminSeeder.SeedAsync(app);

await app.WaitForShutdownAsync(CancellationToken.None);