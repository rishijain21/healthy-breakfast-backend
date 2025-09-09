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

        // ✅ NEW: User-Centric Wallet Endpoints using Supabase Auth ID
        [HttpPost("topup-by-auth")]
        public async Task<ActionResult<UserDto>> TopUpWalletByAuth([FromBody] WalletTopUpRequest request)
        {
            try
            {
                // Get current user ID from Supabase auth ID
                var currentUserService = HttpContext.RequestServices.GetRequiredService<ICurrentUserService>();
                var userId = await currentUserService.GetCurrentUserIdAsync(request.AuthId);
                
                if (!userId.HasValue)
                    return NotFound("User not found");

                var updatedUser = await _walletTransactionService.TopUpWalletAsync(userId.Value, request.Amount, request.Description);
                return Ok(updatedUser);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("balance-by-auth")]
        public async Task<ActionResult<object>> GetWalletBalanceByAuth([FromQuery] Guid authId)
        {
            try
            {
                var currentUserService = HttpContext.RequestServices.GetRequiredService<ICurrentUserService>();
                var userId = await currentUserService.GetCurrentUserIdAsync(authId);
                
                if (!userId.HasValue)
                    return NotFound("User not found");

                var balance = await _walletTransactionService.GetWalletBalanceAsync(userId.Value);
                return Ok(new { balance, userId = userId.Value });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("transactions-by-auth")]
        public async Task<ActionResult<IEnumerable<WalletTransactionDto>>> GetUserTransactionsByAuth([FromQuery] Guid authId)
        {
            try
            {
                var currentUserService = HttpContext.RequestServices.GetRequiredService<ICurrentUserService>();
                var userId = await currentUserService.GetCurrentUserIdAsync(authId);
                
                if (!userId.HasValue)
                    return NotFound("User not found");

                var transactions = await _walletTransactionService.GetUserTransactionsAsync(userId.Value);
                return Ok(transactions);
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

    // ✅ NEW: DTO for auth-based wallet top-up
    public class WalletTopUpRequest
    {
        public Guid AuthId { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; } = "Wallet top-up";
    }
}
