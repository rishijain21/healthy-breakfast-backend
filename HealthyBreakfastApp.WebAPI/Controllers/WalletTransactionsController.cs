using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HealthyBreakfastApp.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class WalletTransactionsController : ControllerBase
    {
        private readonly IWalletTransactionService _walletTransactionService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUserService _userService;
        private readonly ILogger<WalletTransactionsController> _logger;

        public WalletTransactionsController(
            IWalletTransactionService walletTransactionService,
            ICurrentUserService currentUserService,
            IUserService userService,
            ILogger<WalletTransactionsController> logger)
        {
            _walletTransactionService = walletTransactionService;
            _currentUserService = currentUserService;
            _userService = userService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<WalletTransactionDto>>> GetAllTransactions()
        {
            var transactions = await _walletTransactionService.GetAllTransactionsAsync();
            return Ok(transactions);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<WalletTransactionDto>> GetTransaction(int id)
        {
            var transaction = await _walletTransactionService.GetTransactionByIdAsync(id);
            if (transaction == null) return NotFound();
            return Ok(transaction);
        }

        [HttpGet("user/{userId}")]
        public async Task<ActionResult<IEnumerable<WalletTransactionDto>>> GetUserTransactions(int userId)
        {
            var transactions = await _walletTransactionService.GetUserTransactionsAsync(userId);
            return Ok(transactions);
        }

        [HttpGet("user/{userId}/type/{type}")]
        public async Task<ActionResult<IEnumerable<WalletTransactionDto>>> GetUserTransactionsByType(int userId, string type)
        {
            var transactions = await _walletTransactionService.GetUserTransactionsByTypeAsync(userId, type);
            return Ok(transactions);
        }

        [HttpGet("user/{userId}/balance")]
        public async Task<ActionResult<object>> GetUserBalance(int userId)
        {
            var balance = await _walletTransactionService.GetUserBalanceAsync(userId);
            return Ok(new { userId, balance });
        }

        [HttpGet("user/{userId}/summary")]
        public async Task<ActionResult<UserWalletSummaryDto>> GetUserWalletSummary(int userId)
        {
            var summary = await _walletTransactionService.GetUserWalletSummaryAsync(userId);
            if (summary == null) return NotFound();
            return Ok(summary);
        }

        [HttpPost("user/{userId}/topup")]
        public async Task<ActionResult<WalletTransactionDto>> TopUpWallet(int userId, WalletTopUpDto topUpDto)
        {
            var transaction = await _walletTransactionService.TopUpWalletAsync(userId, topUpDto);
            return CreatedAtAction(nameof(GetTransaction), new { id = transaction.TransactionId }, transaction);
        }

        [HttpPost("user/{userId}/debit")]
        public async Task<ActionResult<WalletTransactionDto>> DebitWallet(int userId, [FromBody] DebitWalletDto debitDto)
        {
            var transaction = await _walletTransactionService.DebitWalletAsync(userId, debitDto.Amount, debitDto.Description);
            return CreatedAtAction(nameof(GetTransaction), new { id = transaction.TransactionId }, transaction);
        }

        [HttpGet("user/{userId}/balance/check")]
        public async Task<ActionResult<object>> CheckSufficientBalance(int userId, [FromQuery] decimal amount)
        {
            var hasSufficientBalance = await _walletTransactionService.HasSufficientBalanceAsync(userId, amount);
            return Ok(new { userId, amount, hasSufficientBalance });
        }

        [HttpGet("balance-by-auth")]
        public async Task<ActionResult<object>> GetWalletBalanceByAuth([FromQuery] string? authId = null)
        {
            try
            {
                // ✅ UNIFIED: Get auth ID using consistent method
                var currentAuthId = GetCurrentAuthId();
                _logger.LogInformation($"📋 WALLET: Retrieved authId: {currentAuthId}");
                
                if (string.IsNullOrEmpty(currentAuthId))
                {
                    return Unauthorized("User not authenticated - no valid auth ID found");
                }
                
                if (!Guid.TryParse(currentAuthId, out var validatedAuthId))
                {
                    return Unauthorized($"Invalid user identifier format: {currentAuthId}");
                }

                var userDto = await _userService.FindOrCreateUserByAuthIdAsync(validatedAuthId, "Test User", "test@example.com");
                var balance = await _walletTransactionService.GetWalletBalanceAsync(userDto.UserId);
                
                _logger.LogInformation($"✅ WALLET: Balance retrieved: {balance} for user {userDto.UserId}");
                return Ok(new { balance, userId = userDto.UserId, authId = validatedAuthId });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ WALLET Error: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred while retrieving wallet balance", details = ex.Message });
            }
        }

        [HttpGet("transactions-by-auth")]
        public async Task<ActionResult<IEnumerable<WalletTransactionDto>>> GetUserTransactionsByAuth()
        {
            try
            {
                var currentAuthId = GetCurrentAuthId();
                if (string.IsNullOrEmpty(currentAuthId) || !Guid.TryParse(currentAuthId, out var authGuid))
                {
                    return Unauthorized("User not authenticated");
                }

                var userDto = await _userService.FindOrCreateUserByAuthIdAsync(authGuid, "Test User", "test@example.com");
                var transactions = await _walletTransactionService.GetUserTransactionsAsync(userDto.UserId);
                return Ok(transactions);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ WALLET TRANSACTIONS Error: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred while retrieving wallet transactions", details = ex.Message });
            }
        }

        [HttpPost("topup-by-auth")]
        public async Task<ActionResult<WalletTransactionDto>> TopUpWalletByAuth([FromBody] WalletTopUpDto topUpDto)
        {
            try
            {
                var currentAuthId = GetCurrentAuthId();
                if (string.IsNullOrEmpty(currentAuthId) || !Guid.TryParse(currentAuthId, out var authGuid))
                {
                    return Unauthorized("User not authenticated");
                }

                var userDto = await _userService.FindOrCreateUserByAuthIdAsync(authGuid, "Test User", "test@example.com");
                var transaction = await _walletTransactionService.TopUpWalletAsync(userDto.UserId, topUpDto);
                return CreatedAtAction(nameof(GetTransaction), new { id = transaction.TransactionId }, transaction);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ WALLET TOPUP Error: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred while topping up wallet", details = ex.Message });
            }
        }

        // ✅ HELPER: Unified method to get auth ID from multiple sources
        private string? GetCurrentAuthId()
        {
            try
            {
                // ✅ METHOD 1: Try CurrentUserService (uses middleware)
                var authIdFromService = _currentUserService.GetAuthId();
                if (!string.IsNullOrEmpty(authIdFromService))
                {
                    _logger.LogInformation($"✅ WALLET: AuthId from CurrentUserService: {authIdFromService}");
                    return authIdFromService;
                }

                // ✅ METHOD 2: Try JWT claims directly (fallback)
                var authIdFromClaims = User.FindFirst("sub")?.Value 
                                    ?? User.FindFirst("user_id")?.Value 
                                    ?? User.FindFirst("id")?.Value
                                    ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (!string.IsNullOrEmpty(authIdFromClaims))
                {
                    _logger.LogInformation($"✅ WALLET: AuthId from JWT claims: {authIdFromClaims}");
                    return authIdFromClaims;
                }

                // ✅ METHOD 3: Try HttpContext items directly
                var authIdFromContext = HttpContext.Items["auth_id"]?.ToString();
                if (!string.IsNullOrEmpty(authIdFromContext))
                {
                    _logger.LogInformation($"✅ WALLET: AuthId from HttpContext: {authIdFromContext}");
                    return authIdFromContext;
                }

                _logger.LogWarning("⚠️ WALLET: No authId found from any source");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ WALLET GetCurrentAuthId error: {ex.Message}");
                return null;
            }
        }
    }

    public class DebitWalletDto
    {
        public decimal Amount { get; set; }
        public string Description { get; set; } = null!;
    }
}
