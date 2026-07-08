using System;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.Extensions.Logging;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Services
{
    public class DailyMaintenanceOrchestrator : IDailyMaintenanceOrchestrator
    {
        private readonly ISubscriptionService _subscriptionService;
        private readonly IScheduledOrderService _scheduledOrderService;
        private readonly ISubscriptionSchedulingService _subscriptionSchedulingService;
        private readonly ILogger<DailyMaintenanceOrchestrator> _logger;

        public DailyMaintenanceOrchestrator(
            ISubscriptionService subscriptionService,
            IScheduledOrderService scheduledOrderService,
            ISubscriptionSchedulingService subscriptionSchedulingService,
            ILogger<DailyMaintenanceOrchestrator> logger)
        {
            _subscriptionService = subscriptionService;
            _scheduledOrderService = scheduledOrderService;
            _subscriptionSchedulingService = subscriptionSchedulingService;
            _logger = logger;
        }

        [DisableConcurrentExecution(timeoutInSeconds: 1800)] // 30 minutes timeout
        public async Task RunDailyMaintenanceAsync()
        {
            _logger.LogInformation("Starting Daily Maintenance Orchestrator");

            try
            {
                _logger.LogInformation("Step 1: Expire Subscriptions");
                await _subscriptionService.ExpireSubscriptionsAsync();

                _logger.LogInformation("Step 2: Update Next Scheduled Dates");
                await _subscriptionService.UpdateNextScheduledDatesAsync();

                _logger.LogInformation("Step 3: Confirm All Scheduled Orders");
                var confirmResult = await _scheduledOrderService.ConfirmAllScheduledOrdersAsync(null);

                _logger.LogInformation("Step 4: Generate Scheduled Orders From Subscriptions");
                await _subscriptionSchedulingService.GenerateScheduledOrdersFromSubscriptionsAsync(null);

                _logger.LogInformation("Daily Maintenance Orchestrator completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Daily Maintenance Orchestrator failed");
                throw;
            }
        }
    }
}
