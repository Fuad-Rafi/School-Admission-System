using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Admission.Api.Data;

/// <summary>
/// Used by the EF Core CLI so migrations can be created without booting the web host
/// or requiring a reachable database.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AdmissionDbContext>
{
    public AdmissionDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=admission;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<AdmissionDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AdmissionDbContext(options);
    }
}
