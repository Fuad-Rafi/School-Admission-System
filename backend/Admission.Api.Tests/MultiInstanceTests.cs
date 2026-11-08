using System.Net;
using Admission.Api.Tests.Support;

namespace Admission.Api.Tests;

[Collection(AdmissionCollection.Name)]
public sealed class MultiInstanceTests : ApiTestBase
{
    public MultiInstanceTests(AdmissionSystemFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task TwoBackendInstances_OneDatabase_NeverOversell()
    {
        const int seats = 40;
        const int requests = 200;

        var created = await TestData.CreateClassAsync(Client, "Multi-instance", seats);

        // Second backend host against the same PostgreSQL database (bonus requirement).
        using var secondFactory = new AdmissionApiFactory();
        using var secondClient = secondFactory.CreateClient();

        var studentIds = Enumerable.Range(0, requests)
            .Select(_ => TestData.NextStudentId())
            .ToArray();

        var statuses = await TestData.ApplyConcurrentlyAcrossClientsAsync(
            Client,
            secondClient,
            created.Id,
            studentIds);

        Assert.Equal(seats, statuses.Count(status => status == HttpStatusCode.Created));
        Assert.Equal(requests - seats, statuses.Count(status => status == HttpStatusCode.Conflict));
        Assert.DoesNotContain(statuses, status => (int)status >= 500);

        var state = await TestData.GetClassAsync(Client, created.Id);
        Assert.Equal(seats, state.HeldSeats);
        Assert.Equal(0, state.AvailableSeats);
        Assert.Equal(seats, await TestData.CountActiveHoldsAsync(Fixture.ConnectionString, created.Id));
    }
}
