using System;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using Microsoft.EntityFrameworkCore;
using Sovva.Infrastructure.Data;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sovva.IntegrationTests.Fixtures;

/// <summary>
/// Shared PostgreSQL container fixture reused across all integration tests in the collection.
/// A single real Postgres instance spins up per test collection; each test class
/// creates its own schema-migrated DbContext on a unique database inside the container.
/// Robust against local environments lacking an active Docker daemon.
/// </summary>
public class PostgresContainerFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    public string ConnectionString { get; private set; } = string.Empty;
    public bool IsDockerAvailable { get; private set; } = true;
    public string SkipReason { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        // 1. Check if an external connection string is provided via environment variable
        var customConnectionString = Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(customConnectionString))
        {
            ConnectionString = customConnectionString;
            IsDockerAvailable = true;
            return;
        }

        // 2. Try to build and start Testcontainers PostgreSql container
        try
        {
            _container = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("sovva_test")
                .WithUsername("test")
                .WithPassword("test_password")
                .WithCleanUp(true)
                .Build();

            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
            IsDockerAvailable = true;
        }
        catch (Exception ex)
        {
            IsDockerAvailable = false;
            SkipReason = $"Docker is not running or misconfigured ({ex.GetType().Name}). Set TEST_CONNECTION_STRING or start Docker daemon to run PostgreSQL integration tests.";
        }
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }

    /// <summary>
    /// Checks availability and throws a clear exception if neither Docker nor a test connection string is active.
    /// </summary>
    public void EnsureAvailable()
    {
        if (!IsDockerAvailable)
        {
            throw new InvalidOperationException(SkipReason);
        }
    }

    /// <summary>
    /// Creates a fresh, migrated AppDbContext for isolation between test classes.
    /// Each test class should call this once in its constructor or InitializeAsync.
    /// </summary>
    public AppDbContext CreateDbContext()
    {
        EnsureAvailable();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString, npgsql =>
            {
                npgsql.EnableRetryOnFailure(3);
            })
            .Options;

        var context = new AppDbContext(options);
        // Apply all migrations to the real PostgreSQL instance
        context.Database.Migrate();
        return context;
    }
}

[CollectionDefinition("PostgresCollection")]
public class PostgresCollection : ICollectionFixture<PostgresContainerFixture>
{
    // Marker class — all integration test classes decorated with [Collection("PostgresCollection")]
    // share a single container instance.
}
