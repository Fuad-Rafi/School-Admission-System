namespace Admission.Api.Services;

public static class StudentId
{
    public const string HeaderName = "X-Student-Id";

    /// <summary>
    /// Reads and validates the 17-digit birth-registration number from the request header.
    /// </summary>
    public static string Require(HttpRequest request)
    {
        var values = request.Headers[HeaderName];
        var value = values.Count == 1 ? values[0] : null;

        if (string.IsNullOrEmpty(value) || value.Length != 17 || !value.All(char.IsAsciiDigit))
        {
            throw ApiException.BadRequest(
                "INVALID_STUDENT_ID",
                "Enter a valid 17-digit birth registration number.");
        }

        return value;
    }
}
