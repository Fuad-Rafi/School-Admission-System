namespace Admission.Api.Data.Entities;

/// <summary>
/// Stored and serialised as the exact uppercase strings required by the API contract.
/// </summary>
public enum HoldStatus
{
    HELD,
    CONFIRMED,
    EXPIRED,
    CANCELLED
}
