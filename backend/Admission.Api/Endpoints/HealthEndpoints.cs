using Admission.Api.Data;

namespace Admission.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health", async (AdmissionDbContext db, CancellationToken cancellationToken) =>
            {
                var canConnect = await db.Database.CanConnectAsync(cancellationToken);

                return canConnect
                    ? Results.Ok(new { status = "ok" })
                    : Results.Json(new { status = "degraded" },
                        statusCode: StatusCodes.Status503ServiceUnavailable);
            })
            .WithName("Health")
            .WithTags("Ops");

        return endpoints;
    }
}
