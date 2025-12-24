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
        private DateTime _lastProcessedDate = DateTime.MinValue;


        public OrderConfirmationService(IServiceProvider serviceProvider, ILogger<OrderConfirmationService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 Order Confirmation Service started (IST timezone)");
            _logger.LogInformation($"🕐 Service started at: {TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IstZone):yyyy-MM-dd HH:mm:ss} IST");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IstZone);
                    var todayIst = istNow.Date;
                    var nextMidnightIst = todayIst.AddDays(1); // Tomorrow midnight IST
                    
                    _logger.LogInformation($"⏰ Current IST time: {istNow:yyyy-MM-dd HH:mm:ss}");
                    _logger.LogInformation($"🎯 Next scheduled processing: {nextMidnightIst:yyyy-MM-dd 00:00:00} IST");
                    
                    var delay = nextMidnightIst - istNow;
                    
                    // Safety check: ensure positive delay
                    if (delay.TotalSeconds <= 0)
                    {
                        _logger.LogWarning("⚠️ Calculated delay is negative, using 1 minute delay");
                        delay = TimeSpan.FromMinutes(1);
                    }
                    
                    var hours = (int)delay.TotalHours;
                    var minutes = (int)(delay.TotalMinutes % 60);
                    _logger.LogInformation($"⏱️  Sleeping for {hours}h {minutes}m until midnight");
                    
                    // Wait until next midnight
                    await Task.Delay(delay, stoppingToken);
                    
                    // ✅ Process orders at midnight
                    _logger.LogInformation("🌙 Midnight reached! Processing scheduled orders...");
                    await ProcessScheduledOrdersAsync();
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("🛑 Order confirmation service was cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Unexpected error in order confirmation service");
                    _logger.LogInformation("⏱️  Retrying in 5 minutes...");
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
            }
            
            _logger.LogInformation("🛑 Order Confirmation Service stopped");
        }


        /// <summary>
        /// Processes scheduled orders - called at midnight IST
        /// Includes duplicate prevention using _lastProcessedDate
        /// </summary>
        private async Task ProcessScheduledOrdersAsync()
        {
            var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IstZone);
            var todayIst = istNow.Date;
            
            // ✅ DUPLICATE PREVENTION: Check if we've already processed today
            if (_lastProcessedDate == todayIst)
            {
                _logger.LogWarning($"⏭️  Already processed orders for {todayIst:yyyy-MM-dd} today. Skipping duplicate processing.");
                return;
            }
            
            _logger.LogInformation("═══════════════════════════════════════════════════════════");
            _logger.LogInformation($"🔄 AUTOMATIC ORDER PROCESSING STARTED");
            _logger.LogInformation($"📅 Processing Date: {todayIst:yyyy-MM-dd}");
            _logger.LogInformation($"🕐 Current Time: {istNow:yyyy-MM-dd HH:mm:ss} IST");
            _logger.LogInformation("═══════════════════════════════════════════════════════════");
            
            using var scope = _serviceProvider.CreateScope();
            var scheduledOrderService = scope.ServiceProvider.GetRequiredService<IScheduledOrderService>();
            
            try
            {
                // ✅ Process tomorrow's orders (for next-day delivery)
                await scheduledOrderService.ConfirmAllScheduledOrdersAsync();
                
                // ✅ Mark as processed
                _lastProcessedDate = todayIst;
                
                _logger.LogInformation("═══════════════════════════════════════════════════════════");
                _logger.LogInformation($"✅ AUTOMATIC ORDER PROCESSING COMPLETED SUCCESSFULLY");
                _logger.LogInformation($"📝 Last Processed Date: {_lastProcessedDate:yyyy-MM-dd}");
                _logger.LogInformation("═══════════════════════════════════════════════════════════");
            }
            catch (Exception ex)
            {
                _logger.LogError("═══════════════════════════════════════════════════════════");
                _logger.LogError($"❌ AUTOMATIC ORDER PROCESSING FAILED");
                _logger.LogError($"Error: {ex.Message}");
                _logger.LogError($"Stack Trace: {ex.StackTrace}");
                _logger.LogError("═══════════════════════════════════════════════════════════");
            }
        }
    }
}
