using Xunit;

namespace Admission.Api.Tests.Support;

[Collection(AdmissionCollection.Name)]
public abstract class ApiTestBase : IAsyncLifetime
{
    protected ApiTestBase(AdmissionSystemFixture fixture)
    {
        Fixture = fixture;
    }

    protected AdmissionSystemFixture Fixture { get; }

    protected HttpClient Client { get; private set; } = null!;

    /// <summary>Expiry tests override this to use the 1-second hold factory.</summary>
    protected virtual bool UseShortHolds => false;

    public virtual async Task InitializeAsync()
    {
        await Fixture.ResetDatabaseAsync();

        var factory = UseShortHolds ? Fixture.ShortHoldFactory : Fixture.DefaultFactory;
        Client = factory.CreateClient();
    }

    public virtual Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }
}
