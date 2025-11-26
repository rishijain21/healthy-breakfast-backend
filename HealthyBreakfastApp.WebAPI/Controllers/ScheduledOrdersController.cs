using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Application.DTOs;

namespace HealthyBreakfastApp.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ScheduledOrdersController : ControllerBase
    {
        private readonly IScheduledOrderService _scheduledOrderService;
        private readonly ILogger<ScheduledOrdersController> _logger;

        public ScheduledOrdersController(
            IScheduledOrderService scheduledOrderService,
            ILogger<ScheduledOrdersController> logger)
        {
            _scheduledOrderService = scheduledOrderService;
            _logger = logger;
        }

        [HttpPost("create-from-meal-builder")]
        public async Task<ActionResult<ScheduledOrderResponseDto>> CreateScheduledOrder([FromBody] CreateScheduledOrderDto dto)
        {
            try
            {
                var authIdClaim = User.FindFirst("sub")?.Value ?? 
                                 User.FindFirst("user_id")?.Value ?? 
                                 User.FindFirst("id")?.Value ??
                                 User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                                 
                _logger.LogInformation($"📋 POST Found authId: {authIdClaim}");
                
                if (string.IsNullOrEmpty(authIdClaim))
                {
                    var allClaims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
                    return Unauthorized($"Missing authentication claims. Available: {string.Join(", ", allClaims)}");
                }
                
                if (!Guid.TryParse(authIdClaim, out var authId))
                {
                    return Unauthorized($"Invalid user identifier format: {authIdClaim}");
                }

                var result = await _scheduledOrderService.CreateScheduledOrderAsync(authId, dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating scheduled order");
                return StatusCode(500, new { message = "An error occurred while creating the scheduled order", details = ex.Message });
            }
        }

        [HttpGet("tomorrow")]
        public async Task<ActionResult<List<ScheduledOrderResponseDto>>> GetTomorrowScheduledOrders()
        {
            try
            {
                var authIdClaim = User.FindFirst("sub")?.Value ?? 
                                 User.FindFirst("user_id")?.Value ?? 
                                 User.FindFirst("id")?.Value ??
                                 User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                                 
                _logger.LogInformation($"📋 GET Found authId: {authIdClaim}");
                
                if (string.IsNullOrEmpty(authIdClaim))
                {
                    var allClaims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
                    return Unauthorized($"Missing authentication claims. Available: {string.Join(", ", allClaims)}");
                }
                
                if (!Guid.TryParse(authIdClaim, out var authId))
                {
                    return Unauthorized($"Invalid user identifier format: {authIdClaim}");
                }

                var tomorrow = DateTime.UtcNow.Date.AddDays(1);
                var orders = await _scheduledOrderService.GetScheduledOrdersForDateAsync(authId, tomorrow);
                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving tomorrow's scheduled orders");
                return StatusCode(500, new { message = "An error occurred while retrieving scheduled orders", details = ex.Message });
            }
        }

        [HttpPut("{id}/modify")]
        public async Task<ActionResult> ModifyScheduledOrder(int id, [FromBody] ModifyScheduledOrderDto dto)
        {
            try
            {
                var authIdClaim = User.FindFirst("sub")?.Value ?? 
                                 User.FindFirst("user_id")?.Value ?? 
                                 User.FindFirst("id")?.Value ??
                                 User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                                 
                _logger.LogInformation($"📋 PUT Found authId: {authIdClaim}");
                
                if (string.IsNullOrEmpty(authIdClaim))
                {
                    var allClaims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
                    return Unauthorized($"Missing authentication claims. Available: {string.Join(", ", allClaims)}");
                }
                
                if (!Guid.TryParse(authIdClaim, out var authId))
                {
                    return Unauthorized($"Invalid user identifier format: {authIdClaim}");
                }

                await _scheduledOrderService.ModifyScheduledOrderAsync(authId, id, dto);
                return Ok(new { message = "Scheduled order modified successfully" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error modifying scheduled order {id}");
                return StatusCode(500, new { message = "An error occurred while modifying the scheduled order", details = ex.Message });
            }
        }

        [HttpDelete("{id}/cancel")]
        public async Task<ActionResult> CancelScheduledOrder(int id)
        {
            try
            {
                var authIdClaim = User.FindFirst("sub")?.Value ?? 
                                 User.FindFirst("user_id")?.Value ?? 
                                 User.FindFirst("id")?.Value ??
                                 User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                                 
                _logger.LogInformation($"📋 DELETE Found authId: {authIdClaim}");
                
                if (string.IsNullOrEmpty(authIdClaim))
                {
                    var allClaims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
                    return Unauthorized($"Missing authentication claims. Available: {string.Join(", ", allClaims)}");
                }
                
                if (!Guid.TryParse(authIdClaim, out var authId))
                {
                    return Unauthorized($"Invalid user identifier format: {authIdClaim}");
                }

                await _scheduledOrderService.CancelScheduledOrderAsync(authId, id);
                return Ok(new { message = "Scheduled order cancelled successfully" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error cancelling scheduled order {id}");
                return StatusCode(500, new { message = "An error occurred while cancelling the scheduled order", details = ex.Message });
            }
        }

        [HttpGet("time-until-midnight")]
        [AllowAnonymous]
        public async Task<ActionResult<int>> GetTimeUntilMidnight()
        {
            try
            {
                var minutes = await _scheduledOrderService.GetTimeUntilMidnightMinutesAsync();
                return Ok(minutes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting time until midnight");
                return StatusCode(500, new { message = "An error occurred while getting time until midnight", details = ex.Message });
            }
        }

        // ============================================================================
        // ✅ NEW: MANUAL PROCESSING ENDPOINT (For testing and catching up missed orders)
        // ============================================================================
        [HttpPost("process-today-manual")]
        [AllowAnonymous] // ⚠️ Remove this in production and add proper authorization
        public async Task<IActionResult> ProcessTodayOrdersManual()
        {
            try
            {
                _logger.LogInformation("🔧 Manual order processing triggered");
                
                await _scheduledOrderService.ConfirmAllScheduledOrdersAsync();
                
                _logger.LogInformation("✅ Manual order processing completed");
                
                return Ok(new 
                { 
                    success = true,
                    message = "Scheduled orders processed successfully",
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to process orders manually");
                return StatusCode(500, new 
                { 
                    success = false,
                    message = "Failed to process orders",
                    error = ex.Message,
                    details = ex.StackTrace
                });
            }
        }
    }
}
