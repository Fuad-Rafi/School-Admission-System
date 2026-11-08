using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Admission.Api.Contracts;
using Npgsql;

namespace Admission.Api.Tests.Support;

internal static class TestData
{
    private static int _studentCounter;

    public static string NextStudentId()
        => "2015" + Interlocked.Increment(ref _studentCounter).ToString("D13");

    public static StringContent JsonBody(object value)
        => new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

    public static async Task<ClassCreatedResponse> CreateClassAsync(
        HttpClient client,
        string name,
        int totalSeats)
    {
        using var response = await client.PostAsync("/classes", JsonBody(new { name, totalSeats }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<ClassCreatedResponse>())!;
    }

    public static Task<HttpResponseMessage> ApplyAsync(
        HttpClient client,
        Guid classId,
        string? studentId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/classes/{classId}/holds");
        if (studentId is not null)
        {
            request.Headers.Add("X-Student-Id", studentId);
        }

        return client.SendAsync(request);
    }

    public static async Task<HoldCreatedResponse> ApplySuccessAsync(
        HttpClient client,
        Guid classId,
        string studentId)
    {
        using var response = await ApplyAsync(client, classId, studentId);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<HoldCreatedResponse>())!;
    }

    public static Task<HttpResponseMessage> ConfirmAsync(
        HttpClient client,
        Guid holdId,
        string? studentId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/holds/{holdId}/confirm");
        if (studentId is not null)
        {
            request.Headers.Add("X-Student-Id", studentId);
        }

        return client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> CancelAsync(
        HttpClient client,
        Guid holdId,
        string? studentId)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/holds/{holdId}");
        if (studentId is not null)
        {
            request.Headers.Add("X-Student-Id", studentId);
        }

        return client.SendAsync(request);
    }

    public static async Task<ClassResponse> GetClassAsync(HttpClient client, Guid classId)
    {
        using var response = await client.GetAsync($"/classes/{classId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<ClassResponse>())!;
    }

    public static async Task<IReadOnlyList<ClassResponse>> GetClassesAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/classes");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<List<ClassResponse>>())!;
    }

    public static async Task<HoldResponse> GetHoldAsync(HttpClient client, Guid holdId)
    {
        using var response = await client.GetAsync($"/holds/{holdId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<HoldResponse>())!;
    }

    public static async Task<IReadOnlyList<MyHoldResponse>> GetMyHoldsAsync(
        HttpClient client,
        string studentId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/me/holds");
        request.Headers.Add("X-Student-Id", studentId);

        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<List<MyHoldResponse>>())!;
    }

    public static async Task<ApiErrorResponse> ReadErrorAsync(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<ApiErrorResponse>())!;

    public static async Task<string?> GetHoldStatusFromDatabaseAsync(
        string connectionString,
        Guid holdId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "SELECT status FROM holds WHERE id = @id",
            connection);
        command.Parameters.AddWithValue("id", holdId);

        return (string?)await command.ExecuteScalarAsync();
    }

    public static async Task<int> CountActiveHoldsAsync(string connectionString, Guid classId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM holds WHERE class_id = @id AND status IN ('HELD', 'CONFIRMED')",
            connection);
        command.Parameters.AddWithValue("id", classId);

        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public static async Task ExpireHoldAsync(string connectionString, Guid holdId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "UPDATE holds SET expires_at = now() - interval '1 second' WHERE id = @id",
            connection);
        command.Parameters.AddWithValue("id", holdId);

        await command.ExecuteNonQueryAsync();
    }

    public static async Task WaitForHoldStatusAsync(
        string connectionString,
        Guid holdId,
        string expectedStatus,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            if (await GetHoldStatusFromDatabaseAsync(connectionString, holdId) == expectedStatus)
            {
                return;
            }

            await Task.Delay(100);
        }

        Assert.Equal(expectedStatus, await GetHoldStatusFromDatabaseAsync(connectionString, holdId));
    }

    public static async Task<HttpStatusCode[]> ApplyConcurrentlyAsync(
        HttpClient client,
        Guid classId,
        IEnumerable<string> studentIds)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var tasks = studentIds.Select(async studentId =>
        {
            await gate.Task;

            using var request = new HttpRequestMessage(HttpMethod.Post, $"/classes/{classId}/holds");
            request.Headers.Add("X-Student-Id", studentId);

            using var response = await client.SendAsync(request);
            return response.StatusCode;
        }).ToArray();

        gate.SetResult();
        return await Task.WhenAll(tasks);
    }

    public static async Task<HttpStatusCode[]> ApplyConcurrentlyAcrossClientsAsync(
        HttpClient firstClient,
        HttpClient secondClient,
        Guid classId,
        IReadOnlyList<string> studentIds)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var tasks = studentIds.Select(async (studentId, index) =>
        {
            await gate.Task;

            var client = index % 2 == 0 ? firstClient : secondClient;

            using var request = new HttpRequestMessage(HttpMethod.Post, $"/classes/{classId}/holds");
            request.Headers.Add("X-Student-Id", studentId);

            using var response = await client.SendAsync(request);
            return response.StatusCode;
        }).ToArray();

        gate.SetResult();
        return await Task.WhenAll(tasks);
    }

    public static async Task<HttpStatusCode[]> ConfirmConcurrentlyAsync(
        HttpClient client,
        Guid holdId,
        string studentId,
        int count)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var tasks = Enumerable.Range(0, count).Select(async _ =>
        {
            await gate.Task;

            using var request = new HttpRequestMessage(HttpMethod.Post, $"/holds/{holdId}/confirm");
            request.Headers.Add("X-Student-Id", studentId);

            using var response = await client.SendAsync(request);
            return response.StatusCode;
        }).ToArray();

        gate.SetResult();
        return await Task.WhenAll(tasks);
    }

    public static JsonElement ParseJson(string json)
        => JsonDocument.Parse(json).RootElement;
}
