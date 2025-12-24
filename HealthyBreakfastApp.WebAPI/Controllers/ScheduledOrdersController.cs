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


        // ============================================================================
        // ✅ FIXED: GET PENDING SCHEDULED ORDERS (not yet processed)
        // ============================================================================
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


                // ✅ FIX: Get IST time for consistent date calculation
                var istZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
                var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
                var tomorrow = istNow.Date.AddDays(1);


                _logger.LogInformation($"🗓️ Looking for PENDING orders scheduled for: {tomorrow:yyyy-MM-dd}");


                // Get all orders for tomorrow
                var allOrders = await _scheduledOrderService.GetScheduledOrdersForDateAsync(authId, tomorrow);

                // ✅ KEY FIX: Filter to show ONLY "scheduled" status (exclude processed, cancelled, failed)
                var pendingOrders = allOrders
                    .Where(order => order.OrderStatus?.ToLower() == "scheduled")
                    .ToList();


                _logger.LogInformation($"📦 Found {pendingOrders.Count} PENDING orders (filtered from {allOrders.Count} total)");


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
        // ✅ MANUAL TESTING ENDPOINT - Simulates midnight auto-confirmation
        // ============================================================================
        [HttpPost("process-today-manual")]
        [AllowAnonymous] // ⚠️ Remove in production
        public async Task<IActionResult> ProcessTodayOrdersManual()
        {
            try
            {
                _logger.LogInformation("🔧 Manual order processing triggered (simulating midnight auto-confirmation)");

                // ✅ Get IST time
                var istZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
                var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);

                // ✅ KEY LOGIC: Process orders scheduled for TOMORROW
                // (Simulates what would happen at midnight tonight)
                var tomorrow = istNow.Date.AddDays(1);

                _logger.LogInformation($"🗓️ Current IST time: {istNow:yyyy-MM-dd HH:mm:ss}");
                _logger.LogInformation($"🗓️ Processing orders scheduled for delivery: {tomorrow:yyyy-MM-dd}");
                _logger.LogInformation($"💡 This simulates auto-confirmation that would run at midnight");

                // ✅ Get tomorrow's scheduled orders
                var scheduledOrders = await _scheduledOrderRepository.GetScheduledOrdersForDateAsync(tomorrow);

                _logger.LogInformation($"📦 Found {scheduledOrders.Count} total orders for {tomorrow:yyyy-MM-dd}");

                // Filter for only "scheduled" status
                var pendingOrders = scheduledOrders
                    .Where(o => o.OrderStatus == "scheduled")
                    .ToList();

                _logger.LogInformation($"📋 Found {pendingOrders.Count} PENDING orders to process");

                if (pendingOrders.Count == 0)
                {
                    return Ok(new
                    {
                        success = true,
                        message = $"No pending orders to process for {tomorrow:yyyy-MM-dd}",
                        processedDate = tomorrow.ToString("yyyy-MM-dd"),
                        ordersFound = scheduledOrders.Count,
                        ordersPending = 0,
                        timestamp = DateTime.UtcNow
                    });
                }

                // ✅ Process each pending order
                int processedCount = 0;
                int failedCount = 0;

                foreach (var scheduledOrder in pendingOrders)
                {
                    try
                    {
                        _logger.LogInformation($"🔄 Processing scheduled order #{scheduledOrder.ScheduledOrderId}");
                        _logger.LogInformation($"   ├─ User: {scheduledOrder.UserId}");
                        _logger.LogInformation($"   ├─ Amount: ₹{scheduledOrder.TotalPrice}");
                        _logger.LogInformation($"   └─ Delivery: {scheduledOrder.ScheduledFor:yyyy-MM-dd}");

                        // Get user
                        var user = await _userRepository.GetByAuthIdAsync(scheduledOrder.AuthId);
                        if (user == null)
                        {
                            _logger.LogWarning($"❌ User not found for order {scheduledOrder.ScheduledOrderId}");
                            scheduledOrder.OrderStatus = "failed";
                            scheduledOrder.CanModify = false;
                            await _scheduledOrderRepository.UpdateAsync(scheduledOrder);
                            failedCount++;
                            continue;
                        }

                        // Check wallet balance
                        if (user.WalletBalance < scheduledOrder.TotalPrice)
                        {
                            _logger.LogWarning($"❌ Insufficient balance for order {scheduledOrder.ScheduledOrderId}");
                            _logger.LogWarning($"   Required: ₹{scheduledOrder.TotalPrice}, Available: ₹{user.WalletBalance}");

                            scheduledOrder.OrderStatus = "cancelled";
                            scheduledOrder.CanModify = false;
                            await _scheduledOrderRepository.UpdateAsync(scheduledOrder);
                            failedCount++;
                            continue;
                        }

                        // Create actual order
                        var createOrderDto = new CreateOrderFromMealBuilderDto
                        {
                            UserId = scheduledOrder.UserId,
                            MealId = 1,
                            SelectedIngredients = scheduledOrder.Ingredients.Select(i => new SelectedIngredientDto
                            {
                                IngredientId = i.IngredientId,
                                Quantity = i.Quantity
                            }).ToList(),
                            ScheduledFor = DateTime.SpecifyKind(scheduledOrder.ScheduledFor, DateTimeKind.Utc),
                            DeliveryAddress = "Default Address",
                            SpecialInstructions = $"Auto-confirmed from scheduled order #{scheduledOrder.ScheduledOrderId}"
                        };

                        var orderResponse = await _orderService.CreateOrderFromMealBuilderAsync(createOrderDto);

                        _logger.LogInformation($"✅ Successfully created order #{orderResponse.OrderId}");
                        _logger.LogInformation($"   ├─ From scheduled order: #{scheduledOrder.ScheduledOrderId}");
                        _logger.LogInformation($"   ├─ Amount charged: ₹{scheduledOrder.TotalPrice}");
                        _logger.LogInformation($"   └─ New wallet balance: ₹{user.WalletBalance - scheduledOrder.TotalPrice}");

                        // Mark as processed
                        scheduledOrder.OrderStatus = "processed";
                        scheduledOrder.CanModify = false;
                        scheduledOrder.ConfirmedAt = DateTime.UtcNow;
                        await _scheduledOrderRepository.UpdateAsync(scheduledOrder);

                        processedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"❌ Failed to process order {scheduledOrder.ScheduledOrderId}");
                        scheduledOrder.OrderStatus = "failed";
                        scheduledOrder.CanModify = false;
                        await _scheduledOrderRepository.UpdateAsync(scheduledOrder);
                        failedCount++;
                    }
                }

                _logger.LogInformation($"✅ Manual processing complete!");
                _logger.LogInformation($"   ├─ Successfully processed: {processedCount}");
                _logger.LogInformation($"   └─ Failed: {failedCount}");

                return Ok(new
                {
                    success = true,
                    message = $"Processed {processedCount} orders for {tomorrow:yyyy-MM-dd}",
                    processedDate = tomorrow.ToString("yyyy-MM-dd"),
                    ordersProcessed = processedCount,
                    ordersFailed = failedCount,
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
