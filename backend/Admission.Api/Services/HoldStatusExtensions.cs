using Admission.Api.Data.Entities;

namespace Admission.Api.Services;

public static class HoldStatusExtensions
{
    /// <summary>
    /// Projects an overdue <see cref="HoldStatus.HELD"/> row as EXPIRED even if the background
    /// sweeper has not persisted the transition yet.
    /// </summary>
    public static string ToApiStatus(this HoldStatus status, DateTimeOffset expiresAt, DateTimeOffset nowUtc)
        => status == HoldStatus.HELD && expiresAt <= nowUtc
            ? nameof(HoldStatus.EXPIRED)
            : status.ToString();
}
