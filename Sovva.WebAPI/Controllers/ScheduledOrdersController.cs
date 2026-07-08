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
using Sovva.Domain.Enums;
using Sovva.WebAPI.Extensions;

namespace Sovva.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("default")]
    [Authorize]
    public class ScheduledOrdersController : ControllerBase
    {
        private readonly IScheduledOrderService _scheduledOrderService;
        private readonly IScheduledOrderRepository _scheduledOrderRepository;
        private readonly IUserRepository _userRepository;
        private readonly IOrderService _orderService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IAppTimeProvider _time;
        private readonly ILogger<ScheduledOrdersController> _logger;
        private readonly ISupabaseStorageService _storageService;

        public ScheduledOrdersController(
            IScheduledOrderService scheduledOrderService,
            IScheduledOrderRepository scheduledOrderRepository,
            IUserRepository userRepository,
            IOrderService orderService,
            ICurrentUserService currentUserService,
            IAppTimeProvider time,
            ILogger<ScheduledOrdersController> logger,
            ISupabaseStorageService storageService)
        {
            _scheduledOrderService = scheduledOrderService;
            _scheduledOrderRepository = scheduledOrderRepository;
            _userRepository = userRepository;
            _orderService = orderService;
            _currentUserService = currentUserService;
            _time = time;
            _logger = logger;
            _storageService = storageService;
        }

        [HttpPost("create-from-meal-builder")]
        public async Task<ActionResult<ScheduledOrderResponseDto>> CreateScheduledOrder([FromBody] CreateScheduledOrderDto dto)
        {
            try
            {
                // ✅ NEW: Get userId from JWT claim (zero DB hit)
                var userId = await _currentUserService.GetCurrentUserIdAsync();
                var authId = GetAuthId();
                if (userId is null || authId is null)
                {
                    return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "Missing authentication claims"));
                }

                var result = await _scheduledOrderService.CreateScheduledOrderAsync(userId.Value, authId.Value, dto);
                return Ok(ApiResponse.Ok(result));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse.Fail("BAD_REQUEST", ex.Message));
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
        var userId = await _currentUserService.GetCurrentUserIdAsync();
        var authId = GetAuthId();
        if (userId is null || authId is null)
        {
            return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "Missing authentication claims"));
        }

        var result = await _scheduledOrderService.DuplicateScheduledOrderAsync(userId.Value, authId.Value, id);
        
        _logger.LogInformation("Successfully duplicated order {OriginalId} to {NewId}", id, result.ScheduledOrderId);
        
        return Ok(ApiResponse.Ok(result));
    }
    catch (InvalidOperationException ex)
    {
        _logger.LogWarning("Duplication failed for order {OrderId}: {ErrorMessage}", id, ex.Message);
        return BadRequest(ApiResponse.Fail("BAD_REQUEST", ex.Message));
    }
}

        [HttpGet("tomorrow")]
        public async Task<ActionResult<List<ScheduledOrderResponseDto>>> GetTomorrowScheduledOrders()
        {
            // ✅ NEW: Get userId from JWT claim (zero DB hit)
            var userId = await _currentUserService.GetCurrentUserIdAsync();
            var authId = GetAuthId();
            if (userId is null || authId is null)
            {
                return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "Missing authentication claims"));
            }

            var istNow = _time.ToIst(_time.UtcNow);
            var tomorrow = istNow.Date.AddDays(1);

            _logger.LogInformation("🗓️ Looking for cart orders scheduled for: {Tomorrow}", tomorrow.ToString("yyyy-MM-dd"));

            var allOrders = await _scheduledOrderService.GetScheduledOrdersForDateAsync(userId.Value, authId.Value, tomorrow);

            var pendingOrders = allOrders
                .Where(order => order.OrderStatus?.ToLower() == "scheduled")
                .ToList();

            // ✅ FIX: Sign raw storage paths for meal images so frontend can render them
            foreach (var order in pendingOrders)
            {
                if (!string.IsNullOrEmpty(order.MealImageUrl))
                {
                    try
                    {
                        order.MealImageUrl = await _storageService.GetSignedUrlAsync(order.MealImageUrl);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Failed to sign image for order {OrderId}: {Error}", order.ScheduledOrderId, ex.Message);
                        order.MealImageUrl = null;
                    }
                }
            }

            _logger.LogInformation("📦 Found {PendingCount} orders in cart (filtered from {TotalCount} total)", pendingOrders.Count, allOrders.Count);

            return Ok(ApiResponse.Ok(pendingOrders));
        }

        [HttpPut("{id}/modify")]
        public async Task<ActionResult> ModifyScheduledOrder(int id, [FromBody] ModifyScheduledOrderDto dto)
        {
            try
            {
                // ✅ NEW: Get userId from JWT claim (zero DB hit)
                var userId = await _currentUserService.GetCurrentUserIdAsync();
                var authId = GetAuthId();
                if (userId is null || authId is null)
                {
                    return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "Missing authentication claims"));
                }

                await _scheduledOrderService.ModifyScheduledOrderAsync(userId.Value, authId.Value, id, dto);
                return Ok(ApiResponse.Ok(new { message = "Scheduled order modified successfully" }));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse.Fail("BAD_REQUEST", ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", ex.Message));
            }
        }

        [HttpDelete("{id}/cancel")]
        public async Task<ActionResult> CancelScheduledOrder(int id)
        {
            try
            {
                // ✅ NEW: Get userId from JWT claim (zero DB hit)
                var userId = await _currentUserService.GetCurrentUserIdAsync();
                var authId = GetAuthId();
                if (userId is null || authId is null)
                {
                    return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "Missing authentication claims"));
                }

                await _scheduledOrderService.CancelScheduledOrderAsync(userId.Value, authId.Value, id);
                return Ok(ApiResponse.Ok(new { message = "Scheduled order cancelled successfully" }));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse.Fail("BAD_REQUEST", ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", ex.Message));
            }
        }

        [HttpGet("time-until-midnight")]
        [AllowAnonymous]
        public async Task<ActionResult<int>> GetTimeUntilMidnight()
        {
            var minutes = await _scheduledOrderService.GetTimeUntilMidnightMinutesAsync();
            return Ok(ApiResponse.Ok(minutes));
        }

        // ============================================================================
        // ✅ NEW: MANUAL PROCESSING ENDPOINTS (Yesterday/Today/Tomorrow)
        // ============================================================================

        [HttpPost("process-today-manual")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ProcessOrdersResponseDto>> ProcessTodayManual()
        {
            var result = await ProcessOrdersForDateAsync(_time.UtcNow, "TODAY");
            return Ok(ApiResponse.Ok(result));
        }

        [HttpPost("process-yesterday-manual")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ProcessOrdersResponseDto>> ProcessYesterdayManual()
        {
            var result = await ProcessOrdersForDateAsync(_time.UtcNow.AddDays(-1), "YESTERDAY");
            return Ok(ApiResponse.Ok(result));
        }

        [HttpPost("process-tomorrow-manual")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ProcessOrdersResponseDto>> ProcessTomorrowManual()
        {
            var result = await ProcessOrdersForDateAsync(_time.UtcNow.AddDays(1), "TOMORROW");
            return Ok(ApiResponse.Ok(result));
        }

        [HttpPost("retry-failed-orders")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ProcessOrdersResponseDto>> RetryFailedOrders([FromQuery] string? date = null)
        {
            DateOnly? targetDate = null;
            if (!string.IsNullOrEmpty(date) && DateOnly.TryParse(date, out var parsed))
            {
                targetDate = parsed;
            }
            
            var result = await _scheduledOrderService.RetryFailedOrdersAsync(targetDate);
            return Ok(ApiResponse.Ok(result));
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
        private async Task<ProcessOrdersResponseDto> ProcessOrdersForDateAsync(DateTime utcDate, string label)
        {
            var istNow     = _time.ToIst(_time.UtcNow);
            var targetIst  = _time.ToIst(utcDate).Date;

            _logger.LogInformation(
                "🧪 [{Label}] Manual processing at {Now:yyyy-MM-dd HH:mm:ss} IST, target date: {Target:yyyy-MM-dd}",
                label, istNow, targetIst);

            // ✅ FIX [S2]: Call the service method instead of direct per-order confirming
            // This ensures atomic wallet deduction correctly applies
            var result = await _scheduledOrderService.ConfirmAllScheduledOrdersAsync(DateOnly.FromDateTime(targetIst));
            
            return result;
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
