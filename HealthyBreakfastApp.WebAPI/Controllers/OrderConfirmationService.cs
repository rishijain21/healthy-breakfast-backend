using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using HealthyBreakfastApp.Application.Interfaces;

namespace HealthyBreakfastApp.WebAPI.Services
{
    public class OrderConfirmationService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OrderConfirmationService> _logger;

        public OrderConfirmationService(IServiceProvider serviceProvider, ILogger<OrderConfirmationService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.UtcNow;
                    var midnight = now.Date.AddDays(1); // Next midnight
                    var delay = midnight - now;
                    
                    _logger.LogInformation($"Next order confirmation at: {midnight} (in {delay.TotalHours:F1} hours)");
                    
                    // Wait until midnight
                    await Task.Delay(delay, stoppingToken);
                    
                    if (!stoppingToken.IsCancellationRequested)
                    {
                        await ConfirmScheduledOrdersAsync();
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Order confirmation service was cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in order confirmation service");
                    // Wait 1 minute before retrying
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }
        }

        private async Task ConfirmScheduledOrdersAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var scheduledOrderService = scope.ServiceProvider.GetRequiredService<IScheduledOrderService>();
            
            try
            {
                _logger.LogInformation("Starting automatic order confirmation process");
                await scheduledOrderService.ConfirmAllScheduledOrdersAsync();
                _logger.LogInformation("Successfully completed automatic order confirmation");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to confirm scheduled orders");
            }
        }
    }
}
