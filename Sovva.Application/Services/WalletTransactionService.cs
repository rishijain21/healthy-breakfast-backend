using System.Collections.Generic;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.DTOs;
using Sovva.Application.Features.Wallet.Commands.AdminCreditWallet;
using Sovva.Application.Features.Wallet.Commands.AtomicDebit;
using Sovva.Application.Features.Wallet.Commands.CreateWalletTransaction;
using Sovva.Application.Features.Wallet.Commands.DebitWallet;
using Sovva.Application.Features.Wallet.Commands.TopUpWallet;
using Sovva.Application.Features.Wallet.Commands.WriteTransactionRecord;
using Sovva.Application.Features.Wallet.Queries.GetBalance;
using Sovva.Application.Features.Wallet.Queries.GetSummary;
using Sovva.Application.Features.Wallet.Queries.GetTransactions;
using Sovva.Application.Features.Wallet.Queries.HasSufficientBalance;
using Sovva.Application.Features.Wallet.Queries.TransactionExistsForScheduledOrder;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Services;

/// <summary>
/// CQRS Facade adapting IWalletTransactionService to MediatR Requests.
/// Delegates all wallet operations to specialized Command and Query handlers inside Features/Wallet/.
/// </summary>
public class WalletTransactionService : IWalletTransactionService
{
    private readonly ISender _sender;

    public WalletTransactionService(ISender sender)
    {
        _sender = sender;
    }

    public async Task<IEnumerable<WalletTransactionDto>> GetAllTransactionsAsync()
        => await _sender.Send(new GetAllTransactionsQuery());

    public async Task<PagedResult<WalletTransactionDto>> GetAllTransactionsPagedAsync(int page, int pageSize)
        => await _sender.Send(new GetAllTransactionsPagedQuery(page, pageSize));

    public async Task<WalletTransactionDto?> GetTransactionByIdAsync(long transactionId)
        => await _sender.Send(new GetTransactionByIdQuery(transactionId));

    public async Task<PagedResult<WalletTransactionDto>> GetUserTransactionsAsync(int userId, int page, int pageSize)
        => await _sender.Send(new GetUserTransactionsPagedQuery(userId, page, pageSize));

    public async Task<IEnumerable<WalletTransactionDto>> GetUserTransactionsByTypeAsync(int userId, string type)
        => await _sender.Send(new GetUserTransactionsByTypeQuery(userId, type));

    public async Task<decimal> GetUserBalanceAsync(int userId)
        => await _sender.Send(new GetUserBalanceQuery(userId));

    public async Task<decimal> GetWalletBalanceAsync(int userId)
        => await _sender.Send(new GetUserBalanceQuery(userId));

    public async Task<UserWalletSummaryDto?> GetUserWalletSummaryAsync(int userId)
        => await _sender.Send(new GetUserWalletSummaryQuery(userId));

    public async Task<WalletTransactionDto> CreateTransactionAsync(CreateWalletTransactionDto createTransactionDto)
        => await _sender.Send(new CreateWalletTransactionCommand(createTransactionDto));

    public async Task<UserDto> TopUpWalletAsync(int userId, decimal amount, string description = "Wallet top-up")
        => await _sender.Send(new TopUpWalletCommand(userId, amount, description));

    public async Task<WalletTransactionDto> TopUpWalletAsync(int userId, WalletTopUpDto topUpDto)
        => await _sender.Send(new TopUpWalletByDtoCommand(userId, topUpDto));

    public async Task<WalletTransactionDto> DebitWalletAsync(int userId, decimal amount, string description)
        => await _sender.Send(new DebitWalletCommand(userId, amount, description));

    public async Task<bool> HasSufficientBalanceAsync(int userId, decimal amount)
        => await _sender.Send(new HasSufficientBalanceQuery(userId, amount));

    public async Task<WalletTransactionDto> AdminCreditWalletAsync(long userId, decimal amount, string description, int adminUserId)
        => await _sender.Send(new AdminCreditWalletCommand(userId, amount, description, adminUserId));

    public async Task WriteTransactionRecordAsync(int userId, decimal amount, string type, string description, int? scheduledOrderId = null)
        => await _sender.Send(new WriteTransactionRecordCommand(userId, amount, type, description, scheduledOrderId));

    public async Task<bool> TransactionExistsForScheduledOrderAsync(int scheduledOrderId)
        => await _sender.Send(new TransactionExistsForScheduledOrderQuery(scheduledOrderId));

    public async Task<(bool Success, long? TransactionId)> AtomicDebitAsync(int userId, decimal amount, string description, int? scheduledOrderId = null)
        => await _sender.Send(new AtomicDebitCommand(userId, amount, description, scheduledOrderId));
}
