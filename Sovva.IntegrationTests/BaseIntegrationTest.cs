using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sovva.Infrastructure.Data;
using Sovva.IntegrationTests.Fixtures;
using Sovva.IntegrationTests.Helpers;
using Xunit;

namespace Sovva.IntegrationTests;

/// <summary>
/// Base class for all PostgreSQL containerized integration tests.
/// Automatically creates and cleans a fresh AppDbContext for every test execution.
/// Inherit from this class instead of manually setting up PostgresContainerFixture and DbSeeder.
/// </summary>
[Collection("PostgresCollection")]
public abstract class BaseIntegrationTest : IAsyncLifetime
{
    protected readonly PostgresContainerFixture _fixture;
    protected AppDbContext _dbContext { get; private set; } = null!;

    public PostgresContainerFixture Fixture => _fixture;
    public AppDbContext DbContext => _dbContext;

    protected BaseIntegrationTest(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public virtual async Task InitializeAsync()
    {
        _dbContext = _fixture.CreateDbContext();
        await DbSeeder.CleanAsync(_dbContext);
    }

    public virtual async Task DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }
    }
}
