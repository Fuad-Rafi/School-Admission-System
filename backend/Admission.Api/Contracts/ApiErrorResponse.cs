namespace Admission.Api.Contracts;

/// <summary>Stable error body: <c>{ "code": "...", "message": "..." }</c>.</summary>
public sealed record ApiErrorResponse(string Code, string Message);
