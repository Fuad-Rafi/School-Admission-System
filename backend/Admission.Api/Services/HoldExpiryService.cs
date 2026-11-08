using Admission.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Admission.Api.Services;

/// <summary>
/// Persists the transition HELD -> EXPIRED so seats are released with no client involvement.
/// Safe to run in several backend processes at once; <c>SKIP LOCKED</c> keeps it out of the way.
/// </summary>
public sealed class HoldExpiryService(
    IServiceScopeFactory scopeFactory,
    AdmissionOptions options,
    ILogger<HoldExpiryService> logger) : BackgroundService
{
    private const string SweepSql = """
        WITH expired AS (
            SELECT id
            FROM holds
            WHERE status = 'HELD'
              AND expires_at <= now()
            ORDER BY expires_at
            LIMIT 500
            FOR UPDATE SKIP LOCKED
        )
        UPDATE holds h
        SET status = 'EXPIRED', ended_at = now()
        FROM expired e
        WHERE h.id = e.id;
        """;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await SweepAsync(stoppingToken);

            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.HoldSweepIntervalSeconds));

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await SweepAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AdmissionDbContext>();

            var affected = await db.Database.ExecuteSqlRawAsync(SweepSql, cancellationToken);
            if (affected > 0)
            {
                logger.LogInformation("Expired {Count} overdue hold(s).", affected);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Hold expiry sweep failed.");
        }
    }
}
