using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sovva.Application.Interfaces;

namespace Sovva.WebAPI.Jobs
{
    public class DailyMaintenanceJob
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DailyMaintenanceJob> _logger;

        public DailyMaintenanceJob(IServiceScopeFactory scopeFactory, ILogger<DailyMaintenanceJob> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task ExecuteAsync()
        {
            _logger.LogInformation("Hangfire: Starting DailyMaintenanceJob");
            await using var scope = _scopeFactory.CreateAsyncScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<IDailyMaintenanceOrchestrator>();
            await orchestrator.RunDailyMaintenanceAsync();
        }
    }
}
