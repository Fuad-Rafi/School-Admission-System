namespace Admission.Api.Services;

/// <summary>
/// A deliberate, user-facing API failure. The error middleware turns it into the documented
/// <c>{ code, message }</c> body and HTTP status.
/// </summary>
public sealed class ApiException : Exception
{
    public int StatusCode { get; }

    public string Code { get; }

    public ApiException(int statusCode, string code, string message)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }

    public static ApiException BadRequest(string code, string message)
        => new(StatusCodes.Status400BadRequest, code, message);

    public static ApiException Forbidden(string message)
        => new(StatusCodes.Status403Forbidden, "FORBIDDEN", message);

    public static ApiException NotFound(string code, string message)
        => new(StatusCodes.Status404NotFound, code, message);

    public static ApiException Conflict(string code, string message)
        => new(StatusCodes.Status409Conflict, code, message);
}
