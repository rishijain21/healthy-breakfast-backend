using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace Sovva.WebAPI.Controllers
{
    /// <summary>
    /// V2 dashboard — lightweight aggregation endpoint.
    /// Returns wallet balance + active subscriptions + tomorrow orders in one call.
    /// Does NOT replace /api/subscriptions (Subscriptions controller) — both coexist.
    /// </summary>
    [ApiController]
    [Route("api/v2/dashboard")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("default")]
    [Authorize]
    public class DashboardV2Controller : ControllerBase
    {
        private readonly IDashboardService _dashboardService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<DashboardV2Controller> _logger;

        public DashboardV2Controller(
            IDashboardService dashboardService,
            ICurrentUserService currentUserService,
            ILogger<DashboardV2Controller> logger)
        {
            _dashboardService = dashboardService;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        /// <summary>
        /// Lightweight dashboard summary — wallet + subscriptions + tomorrow orders.
        /// Replaces 3 separate Angular HTTP calls with 1.
        /// Cached server-side for 90 seconds per user.
        /// </summary>
        [HttpGet("summary")]
        [ProducesResponseType(typeof(DashboardLightDto), 200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetDashboardSummary(CancellationToken ct)
        {
            var userId = await _currentUserService.GetCurrentUserIdAsync();
            if (userId == null)
                return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "User not authenticated"));

            _logger.LogInformation("GET /api/v2/dashboard/summary for user {UserId}", userId.Value);

            var result = await _dashboardService.GetDashboardLightAsync(userId.Value, ct);
            return Ok(ApiResponse.Ok(result));
        }
    }
}
