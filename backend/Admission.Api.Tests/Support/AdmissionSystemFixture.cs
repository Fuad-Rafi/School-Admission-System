using Npgsql;
using Testcontainers.PostgreSql;

namespace Admission.Api.Tests.Support;

[CollectionDefinition(Name)]
public sealed class AdmissionCollection : ICollectionFixture<AdmissionSystemFixture>
{
    public const string Name = "admission-api";
}

/// <summary>
/// Starts one throw-away PostgreSQL container per test run and hosts two API instances:
/// the normal 600-second hold configuration and a 1-second configuration for expiry tests.
/// </summary>
public sealed class AdmissionSystemFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _database;
    private string? _externalConnectionString;
    private AdmissionApiFactory? _defaultFactory;
    private AdmissionApiFactory? _shortHoldFactory;

    private string? _previousConnectionString;
    private string? _previousHoldDuration;
    private string? _previousSweepInterval;

    public string ConnectionString
    {
        get
        {
            var rawConnectionString = _externalConnectionString ?? _database!.GetConnectionString();

            // Keep the shared pool well below PostgreSQL's default 100 client limit when
            // several test hosts plus direct assertions connect to the same database.
            var builder = new NpgsqlConnectionStringBuilder(rawConnectionString)
            {
                MaxPoolSize = 20
            };

            return builder.ConnectionString;
        }
    }

    public AdmissionApiFactory DefaultFactory => _defaultFactory!;

    public AdmissionApiFactory ShortHoldFactory => _shortHoldFactory!;

    public async Task InitializeAsync()
    {
        var externalConnectionString = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(externalConnectionString))
        {
            _database = new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("admission_test")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build();

            await _database.StartAsync();
        }
        else
        {
            // Fallback for environments where Testcontainers cannot run.
            _externalConnectionString = externalConnectionString;
        }

        _previousConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        _previousHoldDuration = Environment.GetEnvironmentVariable("HOLD_DURATION_SECONDS");
        _previousSweepInterval = Environment.GetEnvironmentVariable("HOLD_SWEEP_INTERVAL_SECONDS");

        Environment.SetEnvironmentVariable("ConnectionStrings__Default", ConnectionString);
        Environment.SetEnvironmentVariable("HOLD_SWEEP_INTERVAL_SECONDS", "1");

        // Default hold duration (contract default).
        Environment.SetEnvironmentVariable("HOLD_DURATION_SECONDS", "600");
        _defaultFactory = new AdmissionApiFactory();
        using (var warmUp = _defaultFactory.CreateClient())
        {
            // Creating the client starts the host and applies migrations.
        }

        // Short duration used only by ExpiryTests.
        Environment.SetEnvironmentVariable("HOLD_DURATION_SECONDS", "1");
        _shortHoldFactory = new AdmissionApiFactory();
        using (var warmUp = _shortHoldFactory.CreateClient())
        {
        }

        // Factories created later by individual tests should use the default duration.
        Environment.SetEnvironmentVariable("HOLD_DURATION_SECONDS", "600");
    }

    public async Task ResetDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand("TRUNCATE holds, classes CASCADE", connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        _shortHoldFactory?.Dispose();
        _defaultFactory?.Dispose();

        Environment.SetEnvironmentVariable("ConnectionStrings__Default", _previousConnectionString);
        Environment.SetEnvironmentVariable("HOLD_DURATION_SECONDS", _previousHoldDuration);
        Environment.SetEnvironmentVariable("HOLD_SWEEP_INTERVAL_SECONDS", _previousSweepInterval);

        if (_database is not null)
        {
            await _database.DisposeAsync();
        }
    }
}
