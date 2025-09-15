using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System;

namespace HealthyBreakfastApp.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WalletTransactionsController : ControllerBase
    {
        private readonly IWalletTransactionService _walletTransactionService;

        public WalletTransactionsController(IWalletTransactionService walletTransactionService)
        {
            _walletTransactionService = walletTransactionService;
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
            if (transaction == null)
                return NotFound();

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
        public async Task<ActionResult<decimal>> GetUserBalance(int userId)
        {
            var balance = await _walletTransactionService.GetUserBalanceAsync(userId);
            return Ok(new { userId, balance });
        }

        [HttpGet("user/{userId}/summary")]
        public async Task<ActionResult<UserWalletSummaryDto>> GetUserWalletSummary(int userId)
        {
            var summary = await _walletTransactionService.GetUserWalletSummaryAsync(userId);
            if (summary == null)
                return NotFound();

            return Ok(summary);
        }

        [HttpPost]
        public async Task<ActionResult<WalletTransactionDto>> CreateTransaction(CreateWalletTransactionDto createTransactionDto)
        {
            try
            {
                var transaction = await _walletTransactionService.CreateTransactionAsync(createTransactionDto);
                return CreatedAtAction(nameof(GetTransaction), new { id = transaction.TransactionId }, transaction);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("user/{userId}/topup")]
        public async Task<ActionResult<WalletTransactionDto>> TopUpWallet(int userId, WalletTopUpDto topUpDto)
        {
            try
            {
                var transaction = await _walletTransactionService.TopUpWalletAsync(userId, topUpDto);
                return CreatedAtAction(nameof(GetTransaction), new { id = transaction.TransactionId }, transaction);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("user/{userId}/debit")]
        public async Task<ActionResult<WalletTransactionDto>> DebitWallet(int userId, [FromBody] DebitWalletDto debitDto)
        {
            try
            {
                var transaction = await _walletTransactionService.DebitWalletAsync(userId, debitDto.Amount, debitDto.Description);
                return CreatedAtAction(nameof(GetTransaction), new { id = transaction.TransactionId }, transaction);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("user/{userId}/balance/check")]
        public async Task<ActionResult<bool>> CheckSufficientBalance(int userId, [FromQuery] decimal amount)
        {
            var hasSufficientBalance = await _walletTransactionService.HasSufficientBalanceAsync(userId, amount);
            return Ok(new { userId, amount, hasSufficientBalance });
        }

        // ✅ UPDATED: Simple Auth ID Endpoints (No JWT Required)
        [HttpGet("balance-by-auth")]
        public async Task<ActionResult<object>> GetWalletBalanceByAuth([FromQuery] string authId)
        {
            try
            {
                if (string.IsNullOrEmpty(authId))
                {
                    return BadRequest("authId parameter is required");
                }

                if (!Guid.TryParse(authId, out var authGuid))
                {
                    return BadRequest("Invalid authId format. Must be a valid GUID.");
                }

                // Find or create user automatically
                var userService = HttpContext.RequestServices.GetRequiredService<IUserService>();
                var userDto = await userService.FindOrCreateUserByAuthIdAsync(authGuid, "Test User", "test@example.com");

                var balance = await _walletTransactionService.GetWalletBalanceAsync(userDto.UserId);
                return Ok(new { balance, userId = userDto.UserId, authId = authGuid });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("transactions-by-auth")]
        public async Task<ActionResult<IEnumerable<WalletTransactionDto>>> GetUserTransactionsByAuth([FromQuery] string authId)
        {
            try
            {
                if (string.IsNullOrEmpty(authId))
                {
                    return BadRequest("authId parameter is required");
                }

                if (!Guid.TryParse(authId, out var authGuid))
                {
                    return BadRequest("Invalid authId format. Must be a valid GUID.");
                }

                // Find or create user automatically
                var userService = HttpContext.RequestServices.GetRequiredService<IUserService>();
                var userDto = await userService.FindOrCreateUserByAuthIdAsync(authGuid, "Test User", "test@example.com");

                var transactions = await _walletTransactionService.GetUserTransactionsAsync(userDto.UserId);
                return Ok(transactions);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("topup-by-auth")]
        public async Task<ActionResult<WalletTransactionDto>> TopUpWalletByAuth([FromQuery] string authId, [FromBody] WalletTopUpDto topUpDto)
        {
            try
            {
                if (string.IsNullOrEmpty(authId))
                {
                    return BadRequest("authId parameter is required");
                }

                if (!Guid.TryParse(authId, out var authGuid))
                {
                    return BadRequest("Invalid authId format. Must be a valid GUID.");
                }

                // Find or create user automatically
                var userService = HttpContext.RequestServices.GetRequiredService<IUserService>();
                var userDto = await userService.FindOrCreateUserByAuthIdAsync(authGuid, "Test User", "test@example.com");

                var transaction = await _walletTransactionService.TopUpWalletAsync(userDto.UserId, topUpDto);
                return CreatedAtAction(nameof(GetTransaction), new { id = transaction.TransactionId }, transaction);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }

    // Helper DTO for debit endpoint
    public class DebitWalletDto
    {
        public decimal Amount { get; set; }
        public string Description { get; set; } = null!;
    }

    // DTO for auth-based wallet top-up (kept for backwards compatibility)
    public class WalletTopUpRequest
    {
        public Guid AuthId { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; } = "Wallet top-up";
    }
}
