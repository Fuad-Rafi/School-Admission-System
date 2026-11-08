using System.Net;
using Admission.Api.Tests.Support;

namespace Admission.Api.Tests;

[Collection(AdmissionCollection.Name)]
public sealed class ClassApiTests : ApiTestBase
{
    public ClassApiTests(AdmissionSystemFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task CreateClass_Returns201_AndAppearsInList()
    {
        var created = await TestData.CreateClassAsync(Client, "Class 6 - Morning Shift", 120);

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("Class 6 - Morning Shift", created.Name);
        Assert.Equal(120, created.TotalSeats);

        var classes = await TestData.GetClassesAsync(Client);
        var only = Assert.Single(classes);

        Assert.Equal(created.Id, only.Id);
        Assert.Equal(120, only.TotalSeats);
        Assert.Equal(0, only.HeldSeats);
        Assert.Equal(0, only.ConfirmedSeats);
        Assert.Equal(120, only.AvailableSeats);
    }

    [Theory]
    [InlineData("", 10)]
    [InlineData("   ", 10)]
    [InlineData("Valid name", 0)]
    [InlineData("Valid name", -5)]
    [InlineData("Valid name", 10001)]
    public async Task CreateClass_InvalidInput_Returns400(string name, int totalSeats)
    {
        using var response = await Client.PostAsync(
            "/classes",
            TestData.JsonBody(new { name, totalSeats }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await TestData.ReadErrorAsync(response);
        Assert.Equal("INVALID_REQUEST", error.Code);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
    }

    [Fact]
    public async Task CreateClass_NameTooLong_Returns400()
    {
        using var response = await Client.PostAsync(
            "/classes",
            TestData.JsonBody(new { name = new string('x', 201), totalSeats = 10 }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListClasses_ComputesSeatCounts()
    {
        var created = await TestData.CreateClassAsync(Client, "Counts", 3);

        var holdA = await TestData.ApplySuccessAsync(Client, created.Id, TestData.NextStudentId());
        await TestData.ApplySuccessAsync(Client, created.Id, TestData.NextStudentId());

        using var confirm = await TestData.ConfirmAsync(Client, holdA.HoldId, holdA.StudentId);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        var state = await TestData.GetClassAsync(Client, created.Id);

        Assert.Equal(1, state.HeldSeats);
        Assert.Equal(1, state.ConfirmedSeats);
        Assert.Equal(1, state.AvailableSeats);
        Assert.Equal(3, state.HeldSeats + state.ConfirmedSeats + state.AvailableSeats);
    }

    [Fact]
    public async Task GetClass_Unknown_Returns404()
    {
        using var response = await Client.GetAsync($"/classes/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var error = await TestData.ReadErrorAsync(response);
        Assert.Equal("CLASS_NOT_FOUND", error.Code);
    }

    [Fact]
    public async Task ListClasses_WhenEmpty_ReturnsEmptyArray()
    {
        Assert.Empty(await TestData.GetClassesAsync(Client));
    }
}
