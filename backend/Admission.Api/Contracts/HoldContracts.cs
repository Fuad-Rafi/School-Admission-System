namespace Admission.Api.Contracts;

public sealed record HoldCreatedResponse(
    Guid HoldId,
    Guid ClassId,
    string StudentId,
    string Status,
    DateTimeOffset ExpiresAt);

public sealed record HoldResponse(
    Guid HoldId,
    Guid ClassId,
    string Status,
    DateTimeOffset ExpiresAt);

public sealed record ConfirmHoldResponse(Guid HoldId, string Status);

public sealed record MyHoldResponse(
    Guid HoldId,
    Guid ClassId,
    string ClassName,
    string Status,
    DateTimeOffset ExpiresAt);
