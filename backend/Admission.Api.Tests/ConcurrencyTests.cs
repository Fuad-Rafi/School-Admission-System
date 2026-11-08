using System.Net;
using Admission.Api.Tests.Support;

namespace Admission.Api.Tests;

[Collection(AdmissionCollection.Name)]
public sealed class ConcurrencyTests : ApiTestBase
{
    public ConcurrencyTests(AdmissionSystemFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task ConcurrentApplies_NeverExceedSeats()
    {
        const int seats = 80;
        const int requests = 300;

        var created = await TestData.CreateClassAsync(Client, "Concurrency - Oversell", seats);
        var studentIds = Enumerable.Range(0, requests)
            .Select(_ => TestData.NextStudentId())
            .ToArray();

        var statuses = await TestData.ApplyConcurrentlyAsync(Client, created.Id, studentIds);

        Assert.Equal(seats, statuses.Count(status => status == HttpStatusCode.Created));
        Assert.Equal(requests - seats, statuses.Count(status => status == HttpStatusCode.Conflict));
        Assert.DoesNotContain(statuses, status => (int)status >= 500);

        var state = await TestData.GetClassAsync(Client, created.Id);
        Assert.Equal(seats, state.HeldSeats);
        Assert.Equal(0, state.ConfirmedSeats);
        Assert.Equal(0, state.AvailableSeats);
        Assert.True(state.HeldSeats + state.ConfirmedSeats <= state.TotalSeats);
        Assert.Equal(seats, await TestData.CountActiveHoldsAsync(Fixture.ConnectionString, created.Id));
    }

    [Fact]
    public async Task SameStudent_ConcurrentApplies_OnlyOneActiveHold()
    {
        const int attempts = 10;

        var created = await TestData.CreateClassAsync(Client, "Concurrency - Duplicate", 2);
        var studentId = TestData.NextStudentId();

        var statuses = await TestData.ApplyConcurrentlyAsync(
            Client,
            created.Id,
            Enumerable.Repeat(studentId, attempts));

        Assert.Equal(1, statuses.Count(status => status == HttpStatusCode.Created));
        Assert.Equal(attempts - 1, statuses.Count(status => status == HttpStatusCode.Conflict));
        Assert.Equal(1, await TestData.CountActiveHoldsAsync(Fixture.ConnectionString, created.Id));

        var state = await TestData.GetClassAsync(Client, created.Id);
        Assert.Equal(1, state.HeldSeats);
    }

    [Fact]
    public async Task ConcurrentConfirms_IncrementConfirmedSeatsOnce()
    {
        const int attempts = 10;

        var created = await TestData.CreateClassAsync(Client, "Concurrency - Confirm", 2);
        var studentId = TestData.NextStudentId();
        var hold = await TestData.ApplySuccessAsync(Client, created.Id, studentId);

        var statuses = await TestData.ConfirmConcurrentlyAsync(Client, hold.HoldId, studentId, attempts);

        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.OK, status));

        var state = await TestData.GetClassAsync(Client, created.Id);
        Assert.Equal(0, state.HeldSeats);
        Assert.Equal(1, state.ConfirmedSeats);
        Assert.Equal(1, state.AvailableSeats);
    }
}
