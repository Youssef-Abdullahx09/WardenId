using Swashbuckle.AspNetCore.SwaggerUI;
using WardenId.Gateway.DependencyInjection;
using WardenId.SharedKernel.Infrastructure;
using WardenId.SharedKernel.Presentation;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddOpenTelemetryLogging(builder.Configuration);

builder.Services.AddSharedKernelModule(builder.Configuration);
builder.Services.AddTenancyModule(builder.Configuration);
builder.Services.AddClientManagementModule(builder.Configuration);
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddGateway(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors();

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

await app.RunAsync(CancellationToken.None);