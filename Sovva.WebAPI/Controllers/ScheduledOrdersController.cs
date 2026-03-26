using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Sovva.Application.Interfaces;
using Sovva.Application.Helpers;
using Sovva.Application.DTOs;
using Sovva.Domain.Entities;
using Sovva.WebAPI.Extensions;

namespace Sovva.WebAPI.Controllers
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
                // ✅ NEW: Get userId from JWT claim (zero DB hit)
                var userId = User.GetSovvaUserId();
                var authId = GetAuthId();
                if (userId is null || authId is null)
                {
                    var allClaims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
                    return Unauthorized($"Missing authentication claims. Available: {string.Join(", ", allClaims)}");
                }

                var result = await _scheduledOrderService.CreateScheduledOrderAsync(userId.Value, authId.Value, dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating scheduled order");
                return StatusCode(500, new { message = "An error occurred while creating the scheduled order" });
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
        // ✅ NEW: Get userId from JWT claim (zero DB hit)
        var userId = User.GetSovvaUserId();
        var authId = GetAuthId();
        if (userId is null || authId is null)
        {
            var allClaims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
            return Unauthorized($"Missing authentication claims. Available: {string.Join(", ", allClaims)}");
        }

        var result = await _scheduledOrderService.DuplicateScheduledOrderAsync(userId.Value, authId.Value, id);
        
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
            message = "An error occurred while duplicating the order" 
        });
    }
}

        [HttpGet("tomorrow")]
        public async Task<ActionResult<List<ScheduledOrderResponseDto>>> GetTomorrowScheduledOrders()
        {
            try
            {
                // ✅ NEW: Get userId from JWT claim (zero DB hit)
                var userId = User.GetSovvaUserId();
                var authId = GetAuthId();
                if (userId is null || authId is null)
                {
                    var allClaims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
                    return Unauthorized($"Missing authentication claims. Available: {string.Join(", ", allClaims)}");
                }

                var istNow = TimeZoneHelper.NowIST();
                var tomorrow = istNow.Date.AddDays(1);

                _logger.LogInformation($"🗓️ Looking for cart orders scheduled for: {tomorrow:yyyy-MM-dd}");

                var allOrders = await _scheduledOrderService.GetScheduledOrdersForDateAsync(userId.Value, authId.Value, tomorrow);

                var pendingOrders = allOrders
                    .Where(order => order.OrderStatus?.ToLower() == "scheduled")
                    .ToList();

                _logger.LogInformation($"📦 Found {pendingOrders.Count} orders in cart (filtered from {allOrders.Count} total)");

                return Ok(pendingOrders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving tomorrow's scheduled orders");
                return StatusCode(500, new { message = "An error occurred while retrieving scheduled orders" });
            }
        }

        [HttpPut("{id}/modify")]
        public async Task<ActionResult> ModifyScheduledOrder(int id, [FromBody] ModifyScheduledOrderDto dto)
        {
            try
            {
                // ✅ NEW: Get userId from JWT claim (zero DB hit)
                var userId = User.GetSovvaUserId();
                var authId = GetAuthId();
                if (userId is null || authId is null)
                {
                    var allClaims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
                    return Unauthorized($"Missing authentication claims. Available: {string.Join(", ", allClaims)}");
                }

                await _scheduledOrderService.ModifyScheduledOrderAsync(userId.Value, authId.Value, id, dto);
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
                return StatusCode(500, new { message = "An error occurred while modifying the scheduled order" });
            }
        }

        [HttpDelete("{id}/cancel")]
        public async Task<ActionResult> CancelScheduledOrder(int id)
        {
            try
            {
                // ✅ NEW: Get userId from JWT claim (zero DB hit)
                var userId = User.GetSovvaUserId();
                var authId = GetAuthId();
                if (userId is null || authId is null)
                {
                    var allClaims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
                    return Unauthorized($"Missing authentication claims. Available: {string.Join(", ", allClaims)}");
                }

                await _scheduledOrderService.CancelScheduledOrderAsync(userId.Value, authId.Value, id);
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
                return StatusCode(500, new { message = "An error occurred while cancelling the scheduled order" });
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
                return StatusCode(500, new { message = "An error occurred while getting time until midnight" });
            }
        }

        // ============================================================================
        // ✅ NEW: MANUAL PROCESSING ENDPOINTS (Yesterday/Today/Tomorrow)
        // ============================================================================

        [HttpPost("process-today-manual")]
        [Authorize(Roles = "Admin")]
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
        [Authorize(Roles = "Admin")]
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
        [Authorize(Roles = "Admin")]
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
        //
        // KEY FIX:
        //   Old code passed raw DateTime.UtcNow / UtcNow.AddDays(-1) etc. to
        //   GetScheduledOrdersForDateAsync which treats its input as an IST date.
        //   This caused the query to look for the wrong calendar date.
        //
        //   Fix: always convert to IST first, then pass the IST Date to the repo.
        //   Also uses the correct CreateOrderFromMealBuilderDto fields (MealId snapshot,
        //   OverrideTotalPrice) to match the service-level fix.
        // ============================================================================
        private async Task<ProcessOrdersResponseDto> ProcessOrdersForDate(DateTime utcDate, string label)
        {
            var istNow     = TimeZoneHelper.NowIST();

            // ✅ FIX: Convert the target UTC date to IST to get the correct calendar date
            var targetIst  = TimeZoneHelper.ToIST(utcDate).Date;

            _logger.LogInformation(
                "🧪 [{Label}] Manual processing at {Now:yyyy-MM-dd HH:mm:ss} IST, target date: {Target:yyyy-MM-dd}",
                label, istNow, targetIst);

            // GetScheduledOrdersForDateAsync treats its argument as an IST date — pass IST
            var scheduledOrders = await _scheduledOrderRepository
                .GetScheduledOrdersForDateAsync(targetIst);

            _logger.LogInformation("📦 Found {Count} total orders for {Date:yyyy-MM-dd}",
                scheduledOrders.Count, targetIst);

            var pendingOrders = scheduledOrders
                .Where(o => o.OrderStatus.ToLower() == "scheduled")
                .ToList();

            _logger.LogInformation("📋 {Count} orders pending confirmation", pendingOrders.Count);

            if (pendingOrders.Count == 0)
            {
                var alreadyProcessed = scheduledOrders.Count(o => o.OrderStatus == "processed");
                return new ProcessOrdersResponseDto
                {
                    Success               = true,
                    Message               = $"No pending orders for {targetIst:yyyy-MM-dd}",
                    DeliveryDate          = targetIst,
                    OrdersFound           = scheduledOrders.Count,
                    OrdersPending         = 0,
                    OrdersAlreadyConfirmed = alreadyProcessed,
                    OrdersConfirmed       = 0,
                    OrdersFailed          = 0,
                    Timestamp             = DateTime.UtcNow,
                    Note                  = "Safe to call multiple times — idempotent"
                };
            }

            int confirmedCount = 0;
            int failedCount    = 0;

            foreach (var scheduledOrder in pendingOrders)
            {
                try
                {
                    _logger.LogInformation("🔄 Confirming ScheduledOrder #{Id}", scheduledOrder.ScheduledOrderId);

                    var user = await _userRepository.GetByAuthIdAsync(scheduledOrder.AuthId);
                    if (user == null)
                    {
                        _logger.LogWarning("❌ User not found for order #{Id}", scheduledOrder.ScheduledOrderId);
                        scheduledOrder.OrderStatus = "failed";
                        scheduledOrder.CanModify   = false;
                        await _scheduledOrderRepository.UpdateAsync(scheduledOrder);
                        failedCount++;
                        continue;
                    }

                    // ✅ FIX: Check wallet balance against FRESH database value to prevent race condition
                    var freshUser = await _userRepository.GetByIdAsync(user.UserId);
                    if (freshUser == null || freshUser.WalletBalance < scheduledOrder.TotalPrice)
                    {
                        _logger.LogWarning(
                            "❌ Insufficient balance for order #{Id}: need ₹{Need}, have ₹{Have}",
                            scheduledOrder.ScheduledOrderId, scheduledOrder.TotalPrice, freshUser?.WalletBalance ?? 0);
                        scheduledOrder.OrderStatus = "cancelled";
                        scheduledOrder.CanModify   = false;
                        await _scheduledOrderRepository.UpdateAsync(scheduledOrder);
                        failedCount++;
                        continue;
                    }

                    // ✅ FIX: Use snapshot MealId + OverrideTotalPrice (not hardcoded MealId=1)
                    var createOrderDto = new CreateOrderFromMealBuilderDto
                    {
                        MealId             = scheduledOrder.MealId ?? 1,
                        MealName           = scheduledOrder.MealName,
                        OverrideTotalPrice = scheduledOrder.TotalPrice,
                        SelectedIngredients = scheduledOrder.Ingredients
                            .Select(i => new SelectedIngredientDto
                            {
                                IngredientId = i.IngredientId,
                                Quantity     = i.Quantity,
                                UnitPrice    = i.UnitPrice,
                                TotalPrice   = i.TotalPrice
                            }).ToList(),
                        ScheduledFor = DateTime.SpecifyKind(scheduledOrder.ScheduledFor.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)
                    };

                    // ✅ Use the dedicated confirmation path — no catalogue lookup, no UserMeal creation
                    var orderId = await _orderService.ConfirmScheduledOrderAsync(scheduledOrder);

                    _logger.LogInformation(
                        "✅ ScheduledOrder #{ScheduledId} → Order #{OrderId} (₹{Price})",
                        scheduledOrder.ScheduledOrderId, orderId, scheduledOrder.TotalPrice);

                    // ✅ FIX: Populate audit trail
                    scheduledOrder.OrderStatus        = "processed";
                    scheduledOrder.CanModify          = false;
                    scheduledOrder.ConfirmedAt        = DateTime.UtcNow;
                    scheduledOrder.IsProcessedToOrder = true;
                    scheduledOrder.ConfirmedOrderId   = orderId;
                    await _scheduledOrderRepository.UpdateAsync(scheduledOrder);

                    confirmedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Failed to confirm order #{Id}", scheduledOrder.ScheduledOrderId);
                    scheduledOrder.OrderStatus = "failed";
                    scheduledOrder.CanModify   = false;
                    await _scheduledOrderRepository.UpdateAsync(scheduledOrder);
                    failedCount++;
                }
            }

            _logger.LogInformation(
                "🎉 [{Label}] Done — {Confirmed} confirmed, {Failed} failed",
                label, confirmedCount, failedCount);

            return new ProcessOrdersResponseDto
            {
                Success               = confirmedCount > 0 || failedCount == 0,
                Message               = $"Processed {confirmedCount} orders for {targetIst:yyyy-MM-dd}",
                DeliveryDate          = targetIst,
                OrdersFound           = scheduledOrders.Count,
                OrdersPending         = pendingOrders.Count,
                OrdersAlreadyConfirmed = scheduledOrders.Count - pendingOrders.Count,
                OrdersConfirmed       = confirmedCount,
                OrdersFailed          = failedCount,
                Timestamp             = DateTime.UtcNow,
                Note                  = "Safe to call multiple times — idempotent"
            };
        }

        /// <summary>
        /// ✅ FIX 9: Extracts and validates the auth GUID from the JWT claims.
        /// Returns null if the claim is missing or not a valid GUID.
        /// </summary>
        private Guid? GetAuthId()
        {
            var claim = User.FindFirst("sub")?.Value
                     ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(claim))
                return null;

            return Guid.TryParse(claim, out var guid) ? guid : null;
        }
    }
}
