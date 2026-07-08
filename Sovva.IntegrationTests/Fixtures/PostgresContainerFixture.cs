using DotNet.Testcontainers.Builders;
using Microsoft.EntityFrameworkCore;
using Sovva.Infrastructure.Data;
using Testcontainers.PostgreSql;

namespace Sovva.IntegrationTests.Fixtures;

/// <summary>
/// Shared PostgreSQL container fixture reused across all integration tests in the collection.
/// A single real Postgres instance spins up per test collection; each test class
/// creates its own schema-migrated DbContext on a unique database inside the container.
/// </summary>
public class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("sovva_test")
        .WithUsername("test")
        .WithPassword("test_password")
        .WithCleanUp(true)
        .Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Creates a fresh, migrated AppDbContext for isolation between test classes.
    /// Each test class should call this once in its constructor.
    /// </summary>
    public AppDbContext CreateDbContext()
    {
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
