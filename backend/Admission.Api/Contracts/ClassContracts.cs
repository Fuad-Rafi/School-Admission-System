namespace Admission.Api.Contracts;

public sealed record CreateClassRequest(string? Name, int TotalSeats);

public sealed record ClassCreatedResponse(Guid Id, string Name, int TotalSeats);

public sealed record ClassResponse(
    Guid Id,
    string Name,
    int TotalSeats,
    int HeldSeats,
    int ConfirmedSeats,
    int AvailableSeats);
