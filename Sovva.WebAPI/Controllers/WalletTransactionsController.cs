using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;
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

        // ==================== USER ENDPOINTS ====================

        /// <summary>
        /// ✅ SECURE: Gets wallet balance for the authenticated user
        /// </summary>
        [HttpGet("my-balance")]
        public async Task<ActionResult<object>> GetMyBalance()
        {
            try
            {
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

                var userDto = await _userService.GetUserByAuthIdAsync(validatedAuthId);
                if (userDto == null)
                {
                    return Unauthorized(new { message = "User not found. Please complete registration first." });
                }

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

        /// <summary>
        /// ✅ SECURE: Gets wallet transactions for the authenticated user
        /// </summary>
        [HttpGet("my-transactions")]
        public async Task<ActionResult<IEnumerable<WalletTransactionDto>>> GetMyTransactions()
        {
            try
            {
                var currentAuthId = GetCurrentAuthId();
                if (string.IsNullOrEmpty(currentAuthId) || !Guid.TryParse(currentAuthId, out var authGuid))
                {
                    return Unauthorized("User not authenticated");
                }

                var userDto = await _userService.GetUserByAuthIdAsync(authGuid);
                if (userDto == null)
                {
                    return Unauthorized(new { message = "User not found. Please complete registration first." });
                }

                var transactions = await _walletTransactionService.GetUserTransactionsAsync(userDto.UserId);
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
                var currentAuthId = GetCurrentAuthId();
                if (string.IsNullOrEmpty(currentAuthId) || !Guid.TryParse(currentAuthId, out var authGuid))
                {
                    return Unauthorized("User not authenticated");
                }

                var userDto = await _userService.GetUserByAuthIdAsync(authGuid);
                if (userDto == null)
                {
                    return Unauthorized(new { message = "User not found. Please complete registration first." });
                }

                if (topUpDto.Amount <= 0)
                {
                    return BadRequest(new { message = "Amount must be greater than 0" });
                }

                var transaction = await _walletTransactionService.TopUpWalletAsync(userDto.UserId, topUpDto);
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
                var currentAuthId = GetCurrentAuthId();
                if (string.IsNullOrEmpty(currentAuthId) || !Guid.TryParse(currentAuthId, out var authGuid))
                {
                    return Unauthorized("User not authenticated");
                }

                var userDto = await _userService.GetUserByAuthIdAsync(authGuid);
                if (userDto == null)
                {
                    return Unauthorized(new { message = "User not found. Please complete registration first." });
                }

                var hasSufficientBalance = await _walletTransactionService.HasSufficientBalanceAsync(userDto.UserId, amount);
                var currentBalance = await _walletTransactionService.GetUserBalanceAsync(userDto.UserId);

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
}
