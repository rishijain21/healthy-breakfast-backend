using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        public ScheduledOrdersController(IScheduledOrderService scheduledOrderService)
        {
            _scheduledOrderService = scheduledOrderService;
        }
[HttpPost("create-from-meal-builder")]
public async Task<ActionResult<ScheduledOrderResponseDto>> CreateScheduledOrder([FromBody] CreateScheduledOrderDto dto)
{
    try
    {
        // ✅ USE THE SAME DEBUGGING CODE AS GET METHOD
        var allClaims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
        Console.WriteLine($"Available claims: {string.Join(", ", allClaims)}");
        
        // ✅ TRY MULTIPLE CLAIM TYPES (same as GET method)
        var authIdClaim = User.FindFirst("sub")?.Value ?? 
                         User.FindFirst("user_id")?.Value ?? 
                         User.FindFirst("id")?.Value ??
                         User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                         
        Console.WriteLine($"Found authId: {authIdClaim}");
        
        if (string.IsNullOrEmpty(authIdClaim))
        {
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
        return StatusCode(500, new { message = "An error occurred while creating the scheduled order", details = ex.Message });
    }
}

       [HttpGet("tomorrow")]
public async Task<ActionResult<List<ScheduledOrderResponseDto>>> GetTomorrowScheduledOrders()
{
    try
    {
        // ✅ DEBUG: Log all available claims
        var allClaims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
        Console.WriteLine($"Available claims: {string.Join(", ", allClaims)}");
        
        // ✅ TRY MULTIPLE CLAIM TYPES
        var authIdClaim = User.FindFirst("sub")?.Value ?? 
                         User.FindFirst("user_id")?.Value ?? 
                         User.FindFirst("id")?.Value ??
                         User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                         
        Console.WriteLine($"Found authId: {authIdClaim}");
        
        if (string.IsNullOrEmpty(authIdClaim))
        {
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
        return StatusCode(500, new { message = "An error occurred while retrieving scheduled orders", details = ex.Message });
    }
}

        [HttpPut("{id}/modify")]
        public async Task<ActionResult> ModifyScheduledOrder(int id, [FromBody] ModifyScheduledOrderDto dto)
        {
            try
            {
                // ✅ FIX: Use same claim extraction as working OrdersController
                var authIdClaim = User.FindFirst("sub")?.Value;
                if (string.IsNullOrEmpty(authIdClaim))
                {
                    return Unauthorized("Missing authentication claims");
                }
                
                if (!Guid.TryParse(authIdClaim, out var authId))
                {
                    return Unauthorized("Invalid user identifier format");
                }

                await _scheduledOrderService.ModifyScheduledOrderAsync(authId, id, dto);
                return Ok(new { message = "Scheduled order modified successfully" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while modifying the scheduled order", details = ex.Message });
            }
        }

        [HttpDelete("{id}/cancel")]
        public async Task<ActionResult> CancelScheduledOrder(int id)
        {
            try
            {
                // ✅ FIX: Use same claim extraction as working OrdersController
                var authIdClaim = User.FindFirst("sub")?.Value;
                if (string.IsNullOrEmpty(authIdClaim))
                {
                    return Unauthorized("Missing authentication claims");
                }
                
                if (!Guid.TryParse(authIdClaim, out var authId))
                {
                    return Unauthorized("Invalid user identifier format");
                }

                await _scheduledOrderService.CancelScheduledOrderAsync(authId, id);
                return Ok(new { message = "Scheduled order cancelled successfully" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while cancelling the scheduled order", details = ex.Message });
            }
        }

        [HttpGet("time-until-midnight")]
        [AllowAnonymous] // ✅ This endpoint doesn't need authentication
        public async Task<ActionResult<int>> GetTimeUntilMidnight()
        {
            try
            {
                var minutes = await _scheduledOrderService.GetTimeUntilMidnightMinutesAsync();
                return Ok(minutes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while getting time until midnight", details = ex.Message });
            }
        }
    }
}
