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
        private readonly ICurrentUserService _currentUserService;
        private readonly IUserService _userService;

        public WalletTransactionsController(
            IWalletTransactionService walletTransactionService,
            ICurrentUserService currentUserService,
            IUserService userService)
        {
            _walletTransactionService = walletTransactionService;
            _currentUserService = currentUserService;
            _userService = userService;
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
        public async Task<ActionResult<object>> GetWalletBalanceByAuth()
        {
            var authId = _currentUserService.GetAuthId();
            if (string.IsNullOrEmpty(authId)) return Unauthorized("User not authenticated");
            if (!Guid.TryParse(authId, out var authGuid)) return BadRequest("Invalid authId format");

            var userDto = await _userService.FindOrCreateUserByAuthIdAsync(authGuid, "Test User", "test@example.com");
            var balance = await _walletTransactionService.GetWalletBalanceAsync(userDto.UserId);
            return Ok(new { balance, userId = userDto.UserId, authId = authGuid });
        }

        [HttpGet("transactions-by-auth")]
        public async Task<ActionResult<IEnumerable<WalletTransactionDto>>> GetUserTransactionsByAuth()
        {
            var authId = _currentUserService.GetAuthId();
            if (string.IsNullOrEmpty(authId)) return Unauthorized("User not authenticated");
            if (!Guid.TryParse(authId, out var authGuid)) return BadRequest("Invalid authId format");

            var userDto = await _userService.FindOrCreateUserByAuthIdAsync(authGuid, "Test User", "test@example.com");
            var transactions = await _walletTransactionService.GetUserTransactionsAsync(userDto.UserId);
            return Ok(transactions);
        }

        [HttpPost("topup-by-auth")]
        public async Task<ActionResult<WalletTransactionDto>> TopUpWalletByAuth([FromBody] WalletTopUpDto topUpDto)
        {
            var authId = _currentUserService.GetAuthId();
            if (string.IsNullOrEmpty(authId)) return Unauthorized("User not authenticated");
            if (!Guid.TryParse(authId, out var authGuid)) return BadRequest("Invalid authId format");

            var userDto = await _userService.FindOrCreateUserByAuthIdAsync(authGuid, "Test User", "test@example.com");
            var transaction = await _walletTransactionService.TopUpWalletAsync(userDto.UserId, topUpDto);
            return CreatedAtAction(nameof(GetTransaction), new { id = transaction.TransactionId }, transaction);
        }
    }

    public class DebitWalletDto
    {
        public decimal Amount { get; set; }
        public string Description { get; set; } = null!;
    }
}
