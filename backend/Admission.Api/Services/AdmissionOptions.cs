namespace Admission.Api.Services;

/// <summary>
/// Application settings read from environment variables / configuration.
/// Invalid or missing values fall back to the documented defaults.
/// </summary>
public sealed record AdmissionOptions(int HoldDurationSeconds, int HoldSweepIntervalSeconds)
{
    public const int DefaultHoldDurationSeconds = 600;
    public const int DefaultHoldSweepIntervalSeconds = 1;

    private const int MaxHoldDurationSeconds = 7 * 24 * 60 * 60; // 7 days
    private const int MaxSweepIntervalSeconds = 60 * 60;         // 1 hour

    public static AdmissionOptions FromConfiguration(IConfiguration configuration)
        => new(
            ParsePositiveInt(configuration["HOLD_DURATION_SECONDS"], DefaultHoldDurationSeconds, MaxHoldDurationSeconds),
            ParsePositiveInt(configuration["HOLD_SWEEP_INTERVAL_SECONDS"], DefaultHoldSweepIntervalSeconds, MaxSweepIntervalSeconds));

    private static int ParsePositiveInt(string? raw, int fallback, int max)
        => int.TryParse(raw, out var value) && value > 0 && value <= max ? value : fallback;
}
