using Microsoft.EntityFrameworkCore;

namespace Admission.Api.Data;

/// <summary>
/// Waits for PostgreSQL to become available and applies pending migrations.
/// Docker Compose already gates startup on a database healthcheck; this retry loop
/// covers the small remaining race (and local development without Compose).
/// </summary>
public static class DatabaseInitializer
{
    private const int MaxAttempts = 10;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    public static async Task InitializeAsync(
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var scope = services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AdmissionDbContext>();

                await db.Database.MigrateAsync(cancellationToken);
                logger.LogInformation("Database is ready; migrations are up to date.");
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts)
            {
                logger.LogWarning(
                    ex,
                    "Database not ready or migration failed (attempt {Attempt}/{MaxAttempts}). Retrying in {DelaySeconds}s.",
                    attempt,
                    MaxAttempts,
                    RetryDelay.TotalSeconds);

                await Task.Delay(RetryDelay, cancellationToken);
            }
        }
    }
}
