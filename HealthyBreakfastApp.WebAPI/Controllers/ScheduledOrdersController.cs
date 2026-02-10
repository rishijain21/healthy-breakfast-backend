using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Domain.Entities;

namespace HealthyBreakfastApp.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ScheduledOrdersController : ControllerBase
    {
        private readonly IScheduledOrderService _scheduledOrderService;
        private readonly IScheduledOrderRepository _scheduledOrderRepository;
        private readonly IUserRepository _userRepository;
        private readonly IOrderService _orderService;
        private readonly ILogger<ScheduledOrdersController> _logger;

        public ScheduledOrdersController(
            IScheduledOrderService scheduledOrderService,
            IScheduledOrderRepository scheduledOrderRepository,
            IUserRepository userRepository,
            IOrderService orderService,
            ILogger<ScheduledOrdersController> logger)
        {
            _scheduledOrderService = scheduledOrderService;
            _scheduledOrderRepository = scheduledOrderRepository;
            _userRepository = userRepository;
            _orderService = orderService;
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
/// <summary>
/// ✅ DUPLICATE SCHEDULED ORDER - POST /api/ScheduledOrders/{id}/duplicate
/// </summary>
[HttpPost("{id}/duplicate")]
public async Task<ActionResult<ScheduledOrderResponseDto>> DuplicateScheduledOrder(int id)
{
    try
    {
        // Extract auth ID from JWT
        var authIdClaim = User.FindFirst("sub")?.Value ??
                         User.FindFirst("user_id")?.Value ??
                         User.FindFirst("id")?.Value ??
                         User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        _logger.LogInformation($"🔄 POST /duplicate - Found authId: {authIdClaim}");

        if (string.IsNullOrEmpty(authIdClaim))
        {
            var allClaims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
            return Unauthorized($"Missing authentication claims. Available: {string.Join(", ", allClaims)}");
        }

        if (!Guid.TryParse(authIdClaim, out var authId))
        {
            return Unauthorized($"Invalid user identifier format: {authIdClaim}");
        }

        var result = await _scheduledOrderService.DuplicateScheduledOrderAsync(authId, id);
        
        _logger.LogInformation($"✅ Successfully duplicated order #{id} → #{result.ScheduledOrderId}");
        
        return Ok(result);
    }
    catch (InvalidOperationException ex)
    {
        _logger.LogWarning($"⚠️ Duplication failed: {ex.Message}");
        return BadRequest(new { message = ex.Message });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, $"❌ Error duplicating scheduled order {id}");
        return StatusCode(500, new { 
            message = "An error occurred while duplicating the order", 
            details = ex.Message 
        });
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

                _logger.LogInformation($"📋 GET /tomorrow - Found authId: {authIdClaim}");

                if (string.IsNullOrEmpty(authIdClaim))
                {
                    var allClaims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
                    return Unauthorized($"Missing authentication claims. Available: {string.Join(", ", allClaims)}");
                }

                if (!Guid.TryParse(authIdClaim, out var authId))
                {
                    return Unauthorized($"Invalid user identifier format: {authIdClaim}");
                }

                var istZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
                var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
                var tomorrow = istNow.Date.AddDays(1);

                _logger.LogInformation($"🗓️ Looking for cart orders scheduled for: {tomorrow:yyyy-MM-dd}");

                var allOrders = await _scheduledOrderService.GetScheduledOrdersForDateAsync(authId, tomorrow);

                var pendingOrders = allOrders
                    .Where(order => order.OrderStatus?.ToLower() == "scheduled")
                    .ToList();

                _logger.LogInformation($"📦 Found {pendingOrders.Count} orders in cart (filtered from {allOrders.Count} total)");

                return Ok(pendingOrders);
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
        // ✅ NEW: MANUAL PROCESSING ENDPOINTS (Yesterday/Today/Tomorrow)
        // ============================================================================

        [HttpPost("process-today-manual")]
        [AllowAnonymous]
        public async Task<ActionResult<ProcessOrdersResponseDto>> ProcessTodayManual()
        {
            try
            {
                var result = await ProcessOrdersForDate(DateTime.UtcNow, "TODAY");
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to process today's orders");
                return StatusCode(500, new ProcessOrdersResponseDto
                {
                    Success = false,
                    Message = "Failed to process orders",
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        [HttpPost("process-yesterday-manual")]
        [AllowAnonymous]
        public async Task<ActionResult<ProcessOrdersResponseDto>> ProcessYesterdayManual()
        {
            try
            {
                var result = await ProcessOrdersForDate(DateTime.UtcNow.AddDays(-1), "YESTERDAY");
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to process yesterday's orders");
                return StatusCode(500, new ProcessOrdersResponseDto
                {
                    Success = false,
                    Message = "Failed to process orders",
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        [HttpPost("process-tomorrow-manual")]
        [AllowAnonymous]
        public async Task<ActionResult<ProcessOrdersResponseDto>> ProcessTomorrowManual()
        {
            try
            {
                var result = await ProcessOrdersForDate(DateTime.UtcNow.AddDays(1), "TOMORROW");
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to process tomorrow's orders");
                return StatusCode(500, new ProcessOrdersResponseDto
                {
                    Success = false,
                    Message = "Failed to process orders",
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        // ============================================================================
        // SHARED PROCESSING LOGIC
        // ============================================================================
        private async Task<ProcessOrdersResponseDto> ProcessOrdersForDate(DateTime utcDate, string label)
        {
            var istZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
            var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
            var targetDate = TimeZoneInfo.ConvertTimeFromUtc(utcDate, istZone).Date;

            _logger.LogInformation($"🧪 [{label}] Manual processing started at {istNow:yyyy-MM-dd HH:mm:ss} IST");
            _logger.LogInformation($"🚚 Processing orders for {targetDate:yyyy-MM-dd}");

            var scheduledOrders = await _scheduledOrderRepository.GetScheduledOrdersForDateAsync(targetDate);
            _logger.LogInformation($"📦 Found {scheduledOrders.Count} total orders for {targetDate:yyyy-MM-dd}");

            var pendingOrders = scheduledOrders
                .Where(o => o.OrderStatus.ToLower() == "scheduled")
                .ToList();

            _logger.LogInformation($"📋 {pendingOrders.Count} orders pending confirmation");

            if (pendingOrders.Count == 0)
            {
                var alreadyProcessed = scheduledOrders.Count(o => o.OrderStatus == "processed");
                return new ProcessOrdersResponseDto
                {
                    Success = true,
                    Message = $"No pending orders for {targetDate:yyyy-MM-dd}",
                    DeliveryDate = targetDate,
                    OrdersFound = scheduledOrders.Count,
                    OrdersPending = 0,
                    OrdersAlreadyConfirmed = alreadyProcessed,
                    OrdersConfirmed = 0,
                    OrdersFailed = 0,
                    Timestamp = DateTime.UtcNow,
                    Note = "Safe to call multiple times - no duplicates"
                };
            }

            int confirmedCount = 0;
            int failedCount = 0;

            foreach (var scheduledOrder in pendingOrders)
            {
                try
                {
                    _logger.LogInformation($"🔄 Confirming cart order #{scheduledOrder.ScheduledOrderId}");

                    var user = await _userRepository.GetByAuthIdAsync(scheduledOrder.AuthId);
                    if (user == null)
                    {
                        _logger.LogWarning($"❌ User not found");
                        scheduledOrder.OrderStatus = "failed";
                        scheduledOrder.CanModify = false;
                        await _scheduledOrderRepository.UpdateAsync(scheduledOrder);
                        failedCount++;
                        continue;
                    }

                    if (user.WalletBalance < scheduledOrder.TotalPrice)
                    {
                        _logger.LogWarning($"❌ Insufficient balance: ₹{scheduledOrder.TotalPrice} needed, ₹{user.WalletBalance} available");
                        scheduledOrder.OrderStatus = "cancelled";
                        scheduledOrder.CanModify = false;
                        await _scheduledOrderRepository.UpdateAsync(scheduledOrder);
                        failedCount++;
                        continue;
                    }

                    var createOrderDto = new CreateOrderFromMealBuilderDto
                    {
                        // Note: UserId is passed as separate parameter from JWT-authenticated user
                        MealId = 1,
                        SelectedIngredients = scheduledOrder.Ingredients.Select(i => new SelectedIngredientDto
                        {
                            IngredientId = i.IngredientId,
                            Quantity = i.Quantity
                        }).ToList(),
                        ScheduledFor = DateTime.SpecifyKind(scheduledOrder.ScheduledFor, DateTimeKind.Utc),
                        DeliveryAddress = "Default Address",
                        SpecialInstructions = $"Confirmed from cart #{scheduledOrder.ScheduledOrderId}"
                    };

                    var orderResponse = await _orderService.CreateOrderFromMealBuilderAsync(createOrderDto, scheduledOrder.UserId);

                    _logger.LogInformation($"✅ Order confirmed → Kitchen order #{orderResponse.OrderId}");
                    _logger.LogInformation($"   💰 Charged: ₹{scheduledOrder.TotalPrice}");

                    scheduledOrder.OrderStatus = "processed";
                    scheduledOrder.CanModify = false;
                    scheduledOrder.ConfirmedAt = DateTime.UtcNow;
                    await _scheduledOrderRepository.UpdateAsync(scheduledOrder);

                    confirmedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"❌ Failed to confirm order #{scheduledOrder.ScheduledOrderId}");
                    scheduledOrder.OrderStatus = "failed";
                    scheduledOrder.CanModify = false;
                    await _scheduledOrderRepository.UpdateAsync(scheduledOrder);
                    failedCount++;
                }
            }

            _logger.LogInformation($"🎉 [{label}] Complete: {confirmedCount} confirmed, {failedCount} failed");

            return new ProcessOrdersResponseDto
            {
                Success = true,
                Message = $"✅ Processed {confirmedCount} orders for {targetDate:yyyy-MM-dd}",
                DeliveryDate = targetDate,
                OrdersFound = scheduledOrders.Count,
                OrdersPending = pendingOrders.Count,
                OrdersAlreadyConfirmed = scheduledOrders.Count - pendingOrders.Count,
                OrdersConfirmed = confirmedCount,
                OrdersFailed = failedCount,
                Timestamp = DateTime.UtcNow,
                Note = "Safe to call multiple times - no duplicates"
            };
        }
    }
}
