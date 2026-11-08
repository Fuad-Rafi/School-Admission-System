namespace Admission.Api.Data.Entities;

/// <summary>
/// A time-limited reservation (or confirmed admission) of a seat for a student.
/// </summary>
public sealed class Hold
{
    public Guid Id { get; set; }

    public Guid ClassId { get; set; }

    public AdmissionClass Class { get; set; } = null!;

    /// <summary>17-digit birth-registration number.</summary>
    public string StudentId { get; set; } = string.Empty;

    public HoldStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? ConfirmedAt { get; set; }

    /// <summary>When the hold reached a terminal state (expired/cancelled/confirmed).</summary>
    public DateTimeOffset? EndedAt { get; set; }
}
