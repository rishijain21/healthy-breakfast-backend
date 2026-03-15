using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Sovva.Application.Interfaces;
using System;
using System.Threading.Tasks;

namespace Sovva.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class KitchenController : ControllerBase
    {
        private readonly IKitchenService _kitchenService;
        private readonly ILogger<KitchenController> _logger;

        public KitchenController(
            IKitchenService kitchenService,
            ILogger<KitchenController> logger)
        {
            _kitchenService = kitchenService;
            _logger = logger;
        }

        /// <summary>
        /// Get all orders that need to be prepared for TODAY's delivery (Kitchen Dashboard)
        /// Shows orders confirmed at midnight for today's morning (7-9 AM) delivery
        /// </summary>
        [HttpGet("today")]
        public async Task<IActionResult> GetTodaysOrders()
        {
            try
            {
                var orders = await _kitchenService.GetOrdersForPreparationAsync();
                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching today's kitchen orders");
                return StatusCode(500, "Error loading kitchen orders");
            }
        }

        /// <summary>
        /// ✨ NEW: Get orders confirmed for TOMORROW's delivery (Pre-planning view)
        /// Shows orders that were just confirmed tonight for next day delivery
        /// </summary>
        [HttpGet("tomorrow")]
        public async Task<IActionResult> GetTomorrowOrders()
        {
            try
            {
                var orders = await _kitchenService.GetOrdersForTomorrowAsync();
                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching tomorrow's kitchen orders");
                return StatusCode(500, "Error loading kitchen orders");
            }
        }

        /// <summary>
        /// Get orders for a specific date (for planning)
        /// Format: YYYY-MM-DD (e.g., 2026-01-10)
        /// </summary>
        [HttpGet("date/{dateString}")]
        public async Task<IActionResult> GetOrdersByDate(string dateString)
        {
            try
            {
                if (!DateTime.TryParse(dateString, out DateTime date))
                {
                    return BadRequest(new 
                    { 
                        success = false, 
                        message = "Invalid date format. Please use YYYY-MM-DD format (e.g., 2026-01-10)" 
                    });
                }

                _logger.LogInformation($"📅 Fetching kitchen orders for date: {date:yyyy-MM-dd}");

                var orders = await _kitchenService.GetOrdersForDateAsync(date);
                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching orders for date: {Date}", dateString);
                return StatusCode(500, "Error loading orders");
            }
        }

        /// <summary>
        /// Mark order as prepared by kitchen
        /// </summary>
        [HttpPut("{orderId}/mark-prepared")]
        public async Task<IActionResult> MarkOrderPrepared(int orderId)
        {
            try
            {
                await _kitchenService.MarkOrderAsPreparedAsync(orderId);
                
                _logger.LogInformation($"✅ Order #{orderId} marked as prepared");
                
                return Ok(new 
                { 
                    success = true, 
                    message = $"Order #{orderId} marked as prepared",
                    orderId = orderId,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation for order {OrderId}", orderId);
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking order {OrderId} as prepared", orderId);
                return StatusCode(500, new { success = false, message = "Error updating order status" });
            }
        }

        /// <summary>
        /// Get aggregated statistics for kitchen dashboard
        /// Shows stats for today's delivery orders
        /// </summary>
        [HttpGet("stats/today")]
        public async Task<IActionResult> GetTodayStats()
        {
            try
            {
                var stats = await _kitchenService.GetTodayStatsAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching kitchen stats");
                return StatusCode(500, "Error loading stats");
            }
        }

        /// <summary>
        /// ✨ NEW: Get aggregated statistics for TOMORROW's delivery orders
        /// </summary>
        [HttpGet("stats/tomorrow")]
        public async Task<IActionResult> GetTomorrowStats()
        {
            try
            {
                var stats = await _kitchenService.GetTomorrowStatsAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching tomorrow's kitchen stats");
                return StatusCode(500, "Error loading stats");
            }
        }
    }
}
