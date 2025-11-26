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
        private static readonly TimeZoneInfo IstZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

        public OrderConfirmationService(IServiceProvider serviceProvider, ILogger<OrderConfirmationService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 Order Confirmation Service started (IST timezone)");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // ✅ FIX: Calculate next midnight in IST
                    var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IstZone);
                    var nextMidnightIst = istNow.Date.AddDays(1); // Tomorrow midnight IST
                    
                    var delay = nextMidnightIst - istNow;
                    
                    _logger.LogInformation($"⏰ Next order confirmation at: {nextMidnightIst:yyyy-MM-dd HH:mm:ss} IST");
                    _logger.LogInformation($"⏱️  Waiting {delay.TotalHours:F2} hours until next run");
                    
                    // Wait until IST midnight
                    await Task.Delay(delay, stoppingToken);
                    
                    if (!stoppingToken.IsCancellationRequested)
                    {
                        await ConfirmScheduledOrdersAsync();
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("🛑 Order confirmation service was cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error in order confirmation service");
                    // Wait 5 minutes before retrying
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
            }
            
            _logger.LogInformation("🛑 Order Confirmation Service stopped");
        }

        private async Task ConfirmScheduledOrdersAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var scheduledOrderService = scope.ServiceProvider.GetRequiredService<IScheduledOrderService>();
            
            try
            {
                _logger.LogInformation("🔄 Starting automatic order confirmation process (IST)");
                await scheduledOrderService.ConfirmAllScheduledOrdersAsync();
                _logger.LogInformation("✅ Successfully completed automatic order confirmation");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to confirm scheduled orders");
            }
        }
    }
}
