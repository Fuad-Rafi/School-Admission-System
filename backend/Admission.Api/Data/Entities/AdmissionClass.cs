namespace Admission.Api.Data.Entities;

/// <summary>
/// A class (more precisely: a class + shift) with its own seat capacity.
/// </summary>
public sealed class AdmissionClass
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int TotalSeats { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Hold> Holds { get; set; } = new List<Hold>();
}
