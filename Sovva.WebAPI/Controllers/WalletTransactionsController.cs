using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;
using Sovva.WebAPI.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Sovva.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("default")]
    [Authorize]
    public class WalletTransactionsController : ControllerBase
    {
        private readonly IWalletTransactionService _walletTransactionService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IDashboardService _dashboardService;
        private readonly ILogger<WalletTransactionsController> _logger;

        public WalletTransactionsController(
            IWalletTransactionService walletTransactionService,
            ICurrentUserService currentUserService,
            IDashboardService dashboardService,
            ILogger<WalletTransactionsController> logger)
        {
            _walletTransactionService = walletTransactionService;
            _currentUserService = currentUserService;
            _dashboardService = dashboardService;
            _logger = logger;
        }

        // ==================== USER ENDPOINTS ====================

        /// <summary>
        /// ✅ SECURE: Gets wallet balance for the authenticated user
        /// </summary>
        [HttpGet("my-balance")]
        public async Task<ActionResult<object>> GetMyBalance()
        {
            // ✅ NEW: Zero DB hit - read userId directly from JWT claim
            var userId = await _currentUserService.GetCurrentUserIdAsync();
            if (userId is null)
            {
                return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "User not authenticated"));
            }

            var balance = await _walletTransactionService.GetWalletBalanceAsync(userId.Value);
            
            _logger.LogInformation("WALLET: Balance retrieved for user {UserId}", userId);
            return Ok(ApiResponse.Ok(new { balance, userId }));
        }

        /// <summary>
        /// ✅ SECURE: Gets wallet transactions for the authenticated user (paginated)
        /// </summary>
        [HttpGet("my-transactions")]
        [ProducesResponseType(typeof(PagedResult<WalletTransactionDto>), 200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<PagedResult<WalletTransactionDto>>> GetMyTransactions(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            // ✅ NEW: Zero DB hit - read userId directly from JWT claim
            var userId = await _currentUserService.GetCurrentUserIdAsync();
            if (userId is null)
            {
                return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "User not authenticated"));
            }

            if (pageSize > 100)
                return BadRequest(ApiResponse.Fail("BAD_REQUEST", "Maximum page size is 100"));

            var result = await _walletTransactionService.GetUserTransactionsAsync(userId.Value, page, pageSize);
            return Ok(ApiResponse.Ok(result));
        }

        /// <summary>
        /// ✅ SECURE: Top up wallet for the authenticated user
        /// </summary>
        [HttpPost("topup")]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("financial")]
        public async Task<ActionResult<WalletTransactionDto>> TopUpMyWallet([FromBody] WalletTopUpDto topUpDto)
        {
            // ✅ NEW: Zero DB hit - read userId directly from JWT claim
            var userId = await _currentUserService.GetCurrentUserIdAsync();
            if (userId is null)
            {
                return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "User not authenticated"));
            }

            if (topUpDto.Amount <= 0)
            {
                return BadRequest(ApiResponse.Fail("BAD_REQUEST", "Amount must be greater than 0"));
            }

            var transaction = await _walletTransactionService.TopUpWalletAsync(userId.Value, topUpDto);
            
            // ✅ FIX: Invalidate dashboard cache so top-up is instantly visible
            await _dashboardService.InvalidateDashboardCacheAsync(userId.Value);
            
            return Ok(ApiResponse.Ok(transaction));
        }

        // ==================== ADMIN ENDPOINTS ====================

        /// <summary>
        /// Admin endpoint: Get all wallet transactions (paginated)
        /// P1-5 FIX: Added pagination to prevent unbounded queries at scale
        /// </summary>
        [HttpGet("admin/all")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(PagedResult<WalletTransactionDto>), 200)]
        public async Task<ActionResult<PagedResult<WalletTransactionDto>>> GetAllTransactions(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            if (pageSize > 100)
                return BadRequest(ApiResponse.Fail("BAD_REQUEST", "Maximum page size is 100"));

            var result = await _walletTransactionService.GetAllTransactionsPagedAsync(page, pageSize);
            
            return Ok(ApiResponse.Ok(result));
        }

        /// <summary>
        /// Admin endpoint: Get specific user's balance
        /// </summary>
        [HttpGet("admin/user/{userId}/balance")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<object>> GetUserBalance(int userId)
        {
            var balance = await _walletTransactionService.GetUserBalanceAsync(userId);
            return Ok(ApiResponse.Ok(new { userId, balance }));
        }

        /// <summary>
        /// Admin endpoint: Get specific user's transactions
        /// </summary>
        [HttpGet("admin/user/{userId}/transactions")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(PagedResult<WalletTransactionDto>), 200)]
        public async Task<ActionResult<PagedResult<WalletTransactionDto>>> GetUserTransactions(
            int userId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _walletTransactionService.GetUserTransactionsAsync(userId, page, pageSize);
            return Ok(ApiResponse.Ok(result));
        }

        /// <summary>
        /// Admin endpoint: Credit specific user's wallet
        /// </summary>
        [HttpPost("admin/user/{userId}/credit")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<WalletTransactionDto>> CreditUserWallet(int userId, [FromBody] WalletTopUpDto dto)
        {
            if (dto.Amount <= 0)
            {
                return BadRequest(ApiResponse.Fail("BAD_REQUEST", "Amount must be greater than 0"));
            }

            var adminUserId = await _currentUserService.GetCurrentUserIdAsync();
            if (adminUserId == null)
                return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "User not authenticated"));

            var transaction = await _walletTransactionService.AdminCreditWalletAsync(
                userId, 
                dto.Amount, 
                dto.Description ?? $"Admin credit of ₹{dto.Amount}",
                adminUserId.Value
            );
            return Ok(ApiResponse.Ok(transaction));
        }

        /// <summary>
        /// Admin endpoint: Get transaction by ID
        /// </summary>
        [HttpGet("admin/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<WalletTransactionDto>> GetTransaction(int id)
        {
            var transaction = await _walletTransactionService.GetTransactionByIdAsync(id);
            if (transaction == null) return NotFound(ApiResponse.Fail("NOT_FOUND", "Resource not found"));
            return Ok(ApiResponse.Ok(transaction));
        }
    }
}
