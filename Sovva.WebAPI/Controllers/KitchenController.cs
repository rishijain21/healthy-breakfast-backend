using Sovva.Application.DTOs;
using Sovva.Application.Helpers;
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
        private readonly IAppTimeProvider _time;
        private readonly ILogger<KitchenController> _logger;

        public KitchenController(
            IKitchenService kitchenService,
            IAppTimeProvider time,
            ILogger<KitchenController> logger)
        {
            _kitchenService = kitchenService;
            _time = time;
            _logger = logger;
        }

        /// <summary>
        /// Get all orders that need to be prepared for TODAY's delivery (Kitchen Dashboard)
        /// Shows orders confirmed at midnight for today's morning (7-9 AM) delivery
        /// </summary>
        [HttpGet("today")]
        public async Task<IActionResult> GetTodaysOrders()
        {
            var orders = await _kitchenService.GetOrdersForPreparationAsync();
            return Ok(ApiResponse.Ok(orders));
        }

        /// <summary>
        /// ✨ NEW: Get orders confirmed for TOMORROW's delivery (Pre-planning view)
        /// Shows orders that were just confirmed tonight for next day delivery
        /// </summary>
        [HttpGet("tomorrow")]
        public async Task<IActionResult> GetTomorrowOrders()
        {
            var orders = await _kitchenService.GetOrdersForTomorrowAsync();
            return Ok(ApiResponse.Ok(orders));
        }

        /// <summary>
        /// Get orders for a specific date (Admin — kitchen planning)
        /// </summary>
        /// <param name="dateString">Date in YYYY-MM-DD format, e.g. 2026-04-22</param>
        [HttpGet("date/{dateString}")]
        public async Task<IActionResult> GetOrdersByDate(string dateString)
        {
            if (!DateTime.TryParseExact(dateString, "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out DateTime date))
            {
                return BadRequest(ApiResponse.Fail("BAD_REQUEST", "Invalid date format. Use YYYY-MM-DD (e.g. 2026-04-22)"));
            }

            _logger.LogInformation("Fetching kitchen orders for date: {Date:yyyy-MM-dd}", date);
            var orders = await _kitchenService.GetOrdersForDateAsync(date);
            return Ok(ApiResponse.Ok(orders));
        }

        /// <summary>
        /// Mark an order as prepared (today's orders only)
        /// </summary>
        [HttpPut("{orderId}/mark-prepared")]
        public async Task<IActionResult> MarkOrderPrepared(int orderId)
        {
            try
            {
                await _kitchenService.MarkOrderAsPreparedAsync(orderId);
                _logger.LogInformation("Order #{OrderId} marked as prepared", orderId);
                return Ok(ApiResponse.Ok(new
                {
                    orderId,
                    isPrepared = true,
                    preparedAt = _time.UtcNow
                }));
            }
            catch (Sovva.Application.Exceptions.OrderNotFoundException ex)
            {
                return NotFound(ApiResponse.Fail("NOT_FOUND", ex.Message));
            }
            catch (Sovva.Application.Exceptions.OrderAlreadyPreparedException ex)
            {
                return Conflict(ApiResponse.Fail("CONFLICT", ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse.Fail("BAD_REQUEST", ex.Message));
            }
        }

        /// <summary>
        /// Get aggregated statistics for kitchen dashboard
        /// Shows stats for today's delivery orders
        /// </summary>
        [HttpGet("stats/today")]
        public async Task<IActionResult> GetTodayStats()
        {
            var stats = await _kitchenService.GetTodayStatsAsync();
            return Ok(ApiResponse.Ok(stats));
        }

        /// <summary>
        /// ✨ NEW: Get aggregated statistics for TOMORROW's delivery orders
        /// </summary>
        [HttpGet("stats/tomorrow")]
        public async Task<IActionResult> GetTomorrowStats()
        {
            var stats = await _kitchenService.GetTomorrowStatsAsync();
            return Ok(ApiResponse.Ok(stats));
        }
    }
}
