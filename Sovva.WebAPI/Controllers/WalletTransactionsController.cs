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
    [Authorize]
    public class WalletTransactionsController : ControllerBase
    {
        private readonly IWalletTransactionService _walletTransactionService;
        private readonly ILogger<WalletTransactionsController> _logger;

        public WalletTransactionsController(
            IWalletTransactionService walletTransactionService,
            ILogger<WalletTransactionsController> logger)
        {
            _walletTransactionService = walletTransactionService;
            _logger = logger;
        }

        // ==================== USER ENDPOINTS ====================

        /// <summary>
        /// ✅ SECURE: Gets wallet balance for the authenticated user
        /// </summary>
        [HttpGet("my-balance")]
        public async Task<ActionResult<object>> GetMyBalance()
        {
            try
            {
                // ✅ NEW: Zero DB hit - read userId directly from JWT claim
                var userId = User.GetSovvaUserId();
                if (userId is null)
                {
                    return Unauthorized("User not authenticated - no valid user ID in token");
                }

                var balance = await _walletTransactionService.GetWalletBalanceAsync(userId.Value);
                
                _logger.LogInformation($"✅ WALLET: Balance retrieved: {balance} for user {userId}");
                return Ok(new { balance, userId });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ WALLET Error: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred while retrieving wallet balance", details = ex.Message });
            }
        }

        /// <summary>
        /// ✅ SECURE: Gets wallet transactions for the authenticated user
        /// </summary>
        [HttpGet("my-transactions")]
        public async Task<ActionResult<IEnumerable<WalletTransactionDto>>> GetMyTransactions()
        {
            try
            {
                // ✅ NEW: Zero DB hit - read userId directly from JWT claim
                var userId = User.GetSovvaUserId();
                if (userId is null)
                {
                    return Unauthorized("User not authenticated");
                }

                var transactions = await _walletTransactionService.GetUserTransactionsAsync(userId.Value);
                return Ok(transactions);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ WALLET TRANSACTIONS Error: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred while retrieving wallet transactions", details = ex.Message });
            }
        }

        /// <summary>
        /// ✅ SECURE: Top up wallet for the authenticated user
        /// </summary>
        [HttpPost("topup")]
        public async Task<ActionResult<WalletTransactionDto>> TopUpMyWallet([FromBody] WalletTopUpDto topUpDto)
        {
            try
            {
                // ✅ NEW: Zero DB hit - read userId directly from JWT claim
                var userId = User.GetSovvaUserId();
                if (userId is null)
                {
                    return Unauthorized("User not authenticated");
                }

                if (topUpDto.Amount <= 0)
                {
                    return BadRequest(new { message = "Amount must be greater than 0" });
                }

                var transaction = await _walletTransactionService.TopUpWalletAsync(userId.Value, topUpDto);
                return Ok(transaction);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ WALLET TOPUP Error: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred while topping up wallet", details = ex.Message });
            }
        }

        /// <summary>
        /// ✅ SECURE: Check if authenticated user has sufficient balance
        /// </summary>
        [HttpGet("check-balance")]
        public async Task<ActionResult<object>> CheckBalance([FromQuery] decimal amount)
        {
            try
            {
                // ✅ NEW: Zero DB hit - read userId directly from JWT claim
                var userId = User.GetSovvaUserId();
                if (userId is null)
                {
                    return Unauthorized("User not authenticated");
                }

                // ✅ FIX 9: Use single GetUserBalanceAsync call instead of double aggregate
                var currentBalance = await _walletTransactionService.GetUserBalanceAsync(userId.Value);
                var hasSufficientBalance = currentBalance >= amount;

                return Ok(new
                {
                    hasSufficientBalance,
                    currentBalance,
                    requiredAmount = amount,
                    shortfall = hasSufficientBalance ? 0 : amount - currentBalance
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ WALLET CHECK Error: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred while checking balance", details = ex.Message });
            }
        }

        // ==================== ADMIN ENDPOINTS ====================

        /// <summary>
        /// Admin endpoint: Get all wallet transactions
        /// </summary>
        [HttpGet("admin/all")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<WalletTransactionDto>>> GetAllTransactions()
        {
            var transactions = await _walletTransactionService.GetAllTransactionsAsync();
            return Ok(transactions);
        }

        /// <summary>
        /// Admin endpoint: Get specific user's balance
        /// </summary>
        [HttpGet("admin/user/{userId}/balance")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<object>> GetUserBalance(int userId)
        {
            try
            {
                var balance = await _walletTransactionService.GetUserBalanceAsync(userId);
                return Ok(new { userId, balance });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Admin endpoint: Get specific user's transactions
        /// </summary>
        [HttpGet("admin/user/{userId}/transactions")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<WalletTransactionDto>>> GetUserTransactions(int userId)
        {
            try
            {
                var transactions = await _walletTransactionService.GetUserTransactionsAsync(userId);
                return Ok(transactions);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Admin endpoint: Credit specific user's wallet
        /// </summary>
        [HttpPost("admin/user/{userId}/credit")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<WalletTransactionDto>> CreditUserWallet(int userId, [FromBody] WalletTopUpDto dto)
        {
            try
            {
                if (dto.Amount <= 0)
                {
                    return BadRequest(new { message = "Amount must be greater than 0" });
                }

                var transaction = await _walletTransactionService.TopUpWalletAsync(userId, dto);
                return Ok(transaction);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Admin endpoint: Get transaction by ID
        /// </summary>
        [HttpGet("admin/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<WalletTransactionDto>> GetTransaction(int id)
        {
            var transaction = await _walletTransactionService.GetTransactionByIdAsync(id);
            if (transaction == null) return NotFound();
            return Ok(transaction);
        }
    }
}
