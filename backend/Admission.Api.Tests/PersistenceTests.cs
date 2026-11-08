using Admission.Api.Tests.Support;

namespace Admission.Api.Tests;

[Collection(AdmissionCollection.Name)]
public sealed class PersistenceTests : ApiTestBase
{
    public PersistenceTests(AdmissionSystemFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task NewHost_OnSameDatabase_SeesExistingData()
    {
        var created = await TestData.CreateClassAsync(Client, "Restart persistence", 5);
        var studentId = TestData.NextStudentId();
        var hold = await TestData.ApplySuccessAsync(Client, created.Id, studentId);

        // A fresh host on the same database simulates a process/container restart.
        using var restarted = new AdmissionApiFactory();
        using var restartedClient = restarted.CreateClient();

        var classes = await TestData.GetClassesAsync(restartedClient);
        var reloadedClass = Assert.Single(classes);

        Assert.Equal(created.Id, reloadedClass.Id);
        Assert.Equal(created.Name, reloadedClass.Name);
        Assert.Equal(1, reloadedClass.HeldSeats);
        Assert.Equal(4, reloadedClass.AvailableSeats);

        var reloadedHold = await TestData.GetHoldAsync(restartedClient, hold.HoldId);
        Assert.Equal(hold.HoldId, reloadedHold.HoldId);
        Assert.Equal(created.Id, reloadedHold.ClassId);
        Assert.Equal("HELD", reloadedHold.Status);
    }
}
