using Admission.Api.Data;
using Admission.Api.Endpoints;
using Admission.Api.Infrastructure.Json;
using Admission.Api.Middleware;
using Admission.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- Configuration -----------------------------------------------------------
var admissionOptions = AdmissionOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(admissionOptions);

var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'Default' is not configured. Set ConnectionStrings__Default.");
}

builder.Services.AddDbContext<AdmissionDbContext>(options =>
    options.UseNpgsql(connectionString));

var corsOrigins = (builder.Configuration["CORS_ORIGINS"]
        ?? "http://localhost:3000,http://localhost:5173")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(cors => cors.AddDefaultPolicy(policy =>
    policy.WithOrigins(corsOrigins)
          .AllowAnyHeader()
          .AllowAnyMethod()));

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new UtcDateTimeOffsetConverter()));

// Route-handler binding failures (e.g. malformed JSON) become BadHttpRequestException,
// which the error middleware turns into the documented JSON error shape.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

builder.Services.AddScoped<SeatService>();
builder.Services.AddHostedService<HoldExpiryService>();

var app = builder.Build();

// --- Pipeline ----------------------------------------------------------------
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseCors();

app.MapHealthEndpoints();
app.MapClassEndpoints();
app.MapHoldEndpoints();

// --- Startup -----------------------------------------------------------------
await DatabaseInitializer.InitializeAsync(app.Services, app.Logger);

app.Run();

// Exposed for WebApplicationFactory in the integration tests.
public partial class Program;
