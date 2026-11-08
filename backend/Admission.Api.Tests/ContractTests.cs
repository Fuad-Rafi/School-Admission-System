using System.Net;
using System.Text.Json;
using Admission.Api.Tests.Support;

namespace Admission.Api.Tests;

[Collection(AdmissionCollection.Name)]
public sealed class ContractTests : ApiTestBase
{
    public ContractTests(AdmissionSystemFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task CreateClass_Response_HasRequiredFields()
    {
        using var response = await Client.PostAsync(
            "/classes",
            TestData.JsonBody(new { name = "Contract class", totalSeats = 5 }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var json = TestData.ParseJson(await response.Content.ReadAsStringAsync());

        Assert.True(json.TryGetProperty("id", out _));
        Assert.True(json.TryGetProperty("name", out _));
        Assert.True(json.TryGetProperty("totalSeats", out _));
    }

    [Fact]
    public async Task ClassList_HasAllSeatFields()
    {
        await TestData.CreateClassAsync(Client, "Contract list", 5);

        using var response = await Client.GetAsync("/classes");
        var items = TestData.ParseJson(await response.Content.ReadAsStringAsync());

        var item = items[0];
        foreach (var field in new[]
                 {
                     "id", "name", "totalSeats", "heldSeats", "confirmedSeats", "availableSeats"
                 })
        {
            Assert.True(item.TryGetProperty(field, out _), $"Missing response field '{field}'.");
        }
    }

    [Fact]
    public async Task HoldResponses_HaveRequiredFields_AndUtcTimestamp()
    {
        var created = await TestData.CreateClassAsync(Client, "Contract hold", 2);
        var studentId = TestData.NextStudentId();

        using var apply = await TestData.ApplyAsync(Client, created.Id, studentId);
        Assert.Equal(HttpStatusCode.Created, apply.StatusCode);

        var applyJson = TestData.ParseJson(await apply.Content.ReadAsStringAsync());

        foreach (var field in new[] { "holdId", "classId", "studentId", "status", "expiresAt" })
        {
            Assert.True(applyJson.TryGetProperty(field, out _), $"Missing response field '{field}'.");
        }

        Assert.Equal("HELD", applyJson.GetProperty("status").GetString());
        Assert.EndsWith("Z", applyJson.GetProperty("expiresAt").GetString());

        var holdId = applyJson.GetProperty("holdId").GetGuid();

        using var get = await Client.GetAsync($"/holds/{holdId}");
        var getJson = TestData.ParseJson(await get.Content.ReadAsStringAsync());

        foreach (var field in new[] { "holdId", "classId", "status", "expiresAt" })
        {
            Assert.True(getJson.TryGetProperty(field, out _), $"Missing response field '{field}'.");
        }

        Assert.Equal("HELD", getJson.GetProperty("status").GetString());

        using var confirm = await TestData.ConfirmAsync(Client, holdId, studentId);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        var confirmJson = TestData.ParseJson(await confirm.Content.ReadAsStringAsync());
        Assert.True(confirmJson.TryGetProperty("holdId", out _));
        Assert.Equal("CONFIRMED", confirmJson.GetProperty("status").GetString());
    }

    [Fact]
    public async Task MyHolds_HasClassNameAndStatusFields()
    {
        var created = await TestData.CreateClassAsync(Client, "Contract mine", 2);
        var studentId = TestData.NextStudentId();
        await TestData.ApplySuccessAsync(Client, created.Id, studentId);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/me/holds");
        request.Headers.Add("X-Student-Id", studentId);

        using var response = await Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = TestData.ParseJson(await response.Content.ReadAsStringAsync());
        var item = items[0];

        foreach (var field in new[] { "holdId", "classId", "className", "status", "expiresAt" })
        {
            Assert.True(item.TryGetProperty(field, out _), $"Missing response field '{field}'.");
        }
    }

    [Fact]
    public async Task ErrorResponse_HasCodeAndMessage()
    {
        using var response = await Client.GetAsync($"/classes/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var json = TestData.ParseJson(await response.Content.ReadAsStringAsync());

        Assert.True(json.TryGetProperty("code", out var code));
        Assert.True(json.TryGetProperty("message", out var message));
        Assert.False(string.IsNullOrWhiteSpace(code.GetString()));
        Assert.False(string.IsNullOrWhiteSpace(message.GetString()));
    }
}
