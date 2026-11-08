using System.Net;
using Admission.Api.Tests.Support;

namespace Admission.Api.Tests;

[Collection(AdmissionCollection.Name)]
public sealed class ExpiryTests : ApiTestBase
{
    public ExpiryTests(AdmissionSystemFixture fixture) : base(fixture)
    {
    }

    protected override bool UseShortHolds => true;

    [Fact]
    public async Task Hold_ExpiresAutomatically_WithoutAnyApiCall()
    {
        var created = await TestData.CreateClassAsync(Client, "Expiry", 1);
        var studentId = TestData.NextStudentId();
        var hold = await TestData.ApplySuccessAsync(Client, created.Id, studentId);

        Assert.Equal("HELD", hold.Status);

        // Deliberately make no HTTP requests while the 1-second hold duration elapses.
        // The background sweeper must persist EXPIRED on its own.
        await TestData.WaitForHoldStatusAsync(
            Fixture.ConnectionString,
            hold.HoldId,
            "EXPIRED",
            TimeSpan.FromSeconds(6));

        var state = await TestData.GetClassAsync(Client, created.Id);
        Assert.Equal(0, state.HeldSeats);
        Assert.Equal(1, state.AvailableSeats);

        using var replacement = await TestData.ApplyAsync(Client, created.Id, TestData.NextStudentId());
        Assert.Equal(HttpStatusCode.Created, replacement.StatusCode);
    }

    [Fact]
    public async Task Confirm_AfterAutomaticExpiry_Returns409()
    {
        var created = await TestData.CreateClassAsync(Client, "Expiry confirm", 1);
        var studentId = TestData.NextStudentId();
        var hold = await TestData.ApplySuccessAsync(Client, created.Id, studentId);

        await TestData.WaitForHoldStatusAsync(
            Fixture.ConnectionString,
            hold.HoldId,
            "EXPIRED",
            TimeSpan.FromSeconds(6));

        using var response = await TestData.ConfirmAsync(Client, hold.HoldId, studentId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var error = await TestData.ReadErrorAsync(response);
        Assert.Equal("HOLD_NOT_CONFIRMABLE", error.Code);

        var state = await TestData.GetClassAsync(Client, created.Id);
        Assert.Equal(0, state.HeldSeats);
        Assert.Equal(1, state.AvailableSeats);
    }
}
