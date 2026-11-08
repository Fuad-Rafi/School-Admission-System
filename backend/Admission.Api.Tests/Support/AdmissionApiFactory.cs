using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Admission.Api.Tests.Support;

/// <summary>
/// Hosts the API in-memory. Connection string and hold settings are supplied through
/// process environment variables by <see cref="AdmissionSystemFixture"/> before the host starts,
/// because the application reads them while building its configuration.
/// </summary>
public sealed class AdmissionApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }
}
