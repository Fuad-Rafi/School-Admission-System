using System.Net;
using Admission.Api.Tests.Support;

namespace Admission.Api.Tests;

[Collection(AdmissionCollection.Name)]
public sealed class HoldApiTests : ApiTestBase
{
    public HoldApiTests(AdmissionSystemFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Apply_Returns201_WithContractFields()
    {
        var created = await TestData.CreateClassAsync(Client, "Apply", 2);
        var studentId = TestData.NextStudentId();
        var before = DateTimeOffset.UtcNow;

        var hold = await TestData.ApplySuccessAsync(Client, created.Id, studentId);

        Assert.NotEqual(Guid.Empty, hold.HoldId);
        Assert.Equal(created.Id, hold.ClassId);
        Assert.Equal(studentId, hold.StudentId);
        Assert.Equal("HELD", hold.Status);
        Assert.True(hold.ExpiresAt > before);
        Assert.True(hold.ExpiresAt <= DateTimeOffset.UtcNow.AddSeconds(605));

        var state = await TestData.GetClassAsync(Client, created.Id);
        Assert.Equal(1, state.HeldSeats);
        Assert.Equal(1, state.AvailableSeats);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234567890123456")]
    [InlineData("123456789012345678")]
    [InlineData("abcdefghijklmnopq")]
    [InlineData("1234 67890123456")]
    public async Task Apply_InvalidStudentId_Returns400(string? studentId)
    {
        var created = await TestData.CreateClassAsync(Client, "Bad header", 1);

        using var response = await TestData.ApplyAsync(Client, created.Id, studentId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await TestData.ReadErrorAsync(response);
        Assert.Equal("INVALID_STUDENT_ID", error.Code);
    }

    [Fact]
    public async Task Apply_UnknownClass_Returns404()
    {
        using var response = await TestData.ApplyAsync(Client, Guid.NewGuid(), TestData.NextStudentId());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var error = await TestData.ReadErrorAsync(response);
        Assert.Equal("CLASS_NOT_FOUND", error.Code);
    }

    [Fact]
    public async Task Apply_WhenFull_Returns409_SeatUnavailable()
    {
        var created = await TestData.CreateClassAsync(Client, "Full", 1);
        await TestData.ApplySuccessAsync(Client, created.Id, TestData.NextStudentId());

        using var response = await TestData.ApplyAsync(Client, created.Id, TestData.NextStudentId());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var error = await TestData.ReadErrorAsync(response);
        Assert.Equal("SEAT_UNAVAILABLE", error.Code);

        var state = await TestData.GetClassAsync(Client, created.Id);
        Assert.Equal(1, state.HeldSeats);
        Assert.Equal(0, state.AvailableSeats);
    }

    [Fact]
    public async Task Apply_DuplicateActiveHold_Returns409()
    {
        var created = await TestData.CreateClassAsync(Client, "Duplicate", 2);
        var studentId = TestData.NextStudentId();
        await TestData.ApplySuccessAsync(Client, created.Id, studentId);

        using var response = await TestData.ApplyAsync(Client, created.Id, studentId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var error = await TestData.ReadErrorAsync(response);
        Assert.Equal("DUPLICATE_ACTIVE_HOLD", error.Code);
    }

    [Fact]
    public async Task Confirm_MovesHeldToConfirmed_AndIsIdempotent()
    {
        var created = await TestData.CreateClassAsync(Client, "Confirm", 1);
        var studentId = TestData.NextStudentId();
        var hold = await TestData.ApplySuccessAsync(Client, created.Id, studentId);

        using var first = await TestData.ConfirmAsync(Client, hold.HoldId, studentId);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var second = await TestData.ConfirmAsync(Client, hold.HoldId, studentId);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var state = await TestData.GetClassAsync(Client, created.Id);
        Assert.Equal(0, state.HeldSeats);
        Assert.Equal(1, state.ConfirmedSeats);
        Assert.Equal(0, state.AvailableSeats);
    }

    [Fact]
    public async Task Confirm_WrongStudent_Returns403_WithoutChangingState()
    {
        var created = await TestData.CreateClassAsync(Client, "Ownership", 1);
        var hold = await TestData.ApplySuccessAsync(Client, created.Id, TestData.NextStudentId());

        using var response = await TestData.ConfirmAsync(Client, hold.HoldId, TestData.NextStudentId());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var error = await TestData.ReadErrorAsync(response);
        Assert.Equal("FORBIDDEN", error.Code);

        var state = await TestData.GetClassAsync(Client, created.Id);
        Assert.Equal(1, state.HeldSeats);
        Assert.Equal(0, state.ConfirmedSeats);
    }

    [Fact]
    public async Task Confirm_UnknownHold_Returns404()
    {
        using var response = await TestData.ConfirmAsync(Client, Guid.NewGuid(), TestData.NextStudentId());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var error = await TestData.ReadErrorAsync(response);
        Assert.Equal("HOLD_NOT_FOUND", error.Code);
    }

    [Fact]
    public async Task Confirm_CancelledHold_Returns409()
    {
        var created = await TestData.CreateClassAsync(Client, "Cancelled confirm", 1);
        var studentId = TestData.NextStudentId();
        var hold = await TestData.ApplySuccessAsync(Client, created.Id, studentId);

        using var cancel = await TestData.CancelAsync(Client, hold.HoldId, studentId);
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);

        using var response = await TestData.ConfirmAsync(Client, hold.HoldId, studentId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var error = await TestData.ReadErrorAsync(response);
        Assert.Equal("HOLD_NOT_CONFIRMABLE", error.Code);
    }

    [Fact]
    public async Task Confirm_ExpiredHold_Returns409_AndPersistsExpired()
    {
        var created = await TestData.CreateClassAsync(Client, "Expired confirm", 1);
        var studentId = TestData.NextStudentId();
        var hold = await TestData.ApplySuccessAsync(Client, created.Id, studentId);

        await TestData.ExpireHoldAsync(Fixture.ConnectionString, hold.HoldId);

        using var response = await TestData.ConfirmAsync(Client, hold.HoldId, studentId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var error = await TestData.ReadErrorAsync(response);
        Assert.Equal("HOLD_NOT_CONFIRMABLE", error.Code);
        Assert.Equal(
            "EXPIRED",
            await TestData.GetHoldStatusFromDatabaseAsync(Fixture.ConnectionString, hold.HoldId));

        var state = await TestData.GetClassAsync(Client, created.Id);
        Assert.Equal(0, state.HeldSeats);
        Assert.Equal(1, state.AvailableSeats);
    }

    [Fact]
    public async Task Cancel_ReleasesSeat_AndIsIdempotent()
    {
        var created = await TestData.CreateClassAsync(Client, "Cancel", 1);
        var studentId = TestData.NextStudentId();
        var hold = await TestData.ApplySuccessAsync(Client, created.Id, studentId);

        using var first = await TestData.CancelAsync(Client, hold.HoldId, studentId);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        using var second = await TestData.CancelAsync(Client, hold.HoldId, studentId);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);

        var state = await TestData.GetClassAsync(Client, created.Id);
        Assert.Equal(0, state.HeldSeats);
        Assert.Equal(1, state.AvailableSeats);

        using var replacement = await TestData.ApplyAsync(Client, created.Id, TestData.NextStudentId());
        Assert.Equal(HttpStatusCode.Created, replacement.StatusCode);
    }

    [Fact]
    public async Task Cancel_ConfirmedHold_Returns409()
    {
        var created = await TestData.CreateClassAsync(Client, "Cancel confirmed", 1);
        var studentId = TestData.NextStudentId();
        var hold = await TestData.ApplySuccessAsync(Client, created.Id, studentId);

        using var confirm = await TestData.ConfirmAsync(Client, hold.HoldId, studentId);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        using var response = await TestData.CancelAsync(Client, hold.HoldId, studentId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var error = await TestData.ReadErrorAsync(response);
        Assert.Equal("HOLD_NOT_CANCELLABLE", error.Code);
    }

    [Fact]
    public async Task Cancel_WrongStudent_Returns403()
    {
        var created = await TestData.CreateClassAsync(Client, "Cancel ownership", 1);
        var hold = await TestData.ApplySuccessAsync(Client, created.Id, TestData.NextStudentId());

        using var response = await TestData.CancelAsync(Client, hold.HoldId, TestData.NextStudentId());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MyHolds_ReturnsAllStatesWithClassName()
    {
        var studentId = TestData.NextStudentId();

        var cancelledClass = await TestData.CreateClassAsync(Client, "Mine - Cancelled", 1);
        var cancelled = await TestData.ApplySuccessAsync(Client, cancelledClass.Id, studentId);
        using (var cancel = await TestData.CancelAsync(Client, cancelled.HoldId, studentId))
        {
            Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        }

        var confirmedClass = await TestData.CreateClassAsync(Client, "Mine - Confirmed", 1);
        var confirmed = await TestData.ApplySuccessAsync(Client, confirmedClass.Id, studentId);
        using (var confirm = await TestData.ConfirmAsync(Client, confirmed.HoldId, studentId))
        {
            Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        }

        var mine = await TestData.GetMyHoldsAsync(Client, studentId);

        Assert.Equal(2, mine.Count);
        Assert.Equal("CANCELLED", Assert.Single(mine, h => h.HoldId == cancelled.HoldId).Status);
        Assert.Equal("CONFIRMED", Assert.Single(mine, h => h.HoldId == confirmed.HoldId).Status);
        Assert.Equal("Mine - Cancelled", Assert.Single(mine, h => h.HoldId == cancelled.HoldId).ClassName);
        Assert.Equal("Mine - Confirmed", Assert.Single(mine, h => h.HoldId == confirmed.HoldId).ClassName);
    }

    [Fact]
    public async Task Reapply_AfterCancel_IsAllowed()
    {
        var created = await TestData.CreateClassAsync(Client, "Re-apply", 1);
        var studentId = TestData.NextStudentId();

        var first = await TestData.ApplySuccessAsync(Client, created.Id, studentId);
        using (var cancel = await TestData.CancelAsync(Client, first.HoldId, studentId))
        {
            Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        }

        var second = await TestData.ApplySuccessAsync(Client, created.Id, studentId);
        Assert.NotEqual(first.HoldId, second.HoldId);
    }

    [Fact]
    public async Task HeldCount_ExcludesOverdueHold()
    {
        var created = await TestData.CreateClassAsync(Client, "Overdue projection", 1);
        var studentId = TestData.NextStudentId();
        var hold = await TestData.ApplySuccessAsync(Client, created.Id, studentId);

        await TestData.ExpireHoldAsync(Fixture.ConnectionString, hold.HoldId);

        var fetched = await TestData.GetHoldAsync(Client, hold.HoldId);
        Assert.Equal("EXPIRED", fetched.Status);

        var state = await TestData.GetClassAsync(Client, created.Id);
        Assert.Equal(0, state.HeldSeats);
        Assert.Equal(1, state.AvailableSeats);
    }
}
