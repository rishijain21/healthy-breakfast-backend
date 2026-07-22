using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Sovva.Application.Common.Infrastructure;
using Sovva.Application.DTOs;
using Sovva.Application.Exceptions;
using Sovva.Application.Interfaces;
using Sovva.Domain.Constants;
using Sovva.Domain.Entities;

namespace Sovva.Application.Features.Wallet.Commands.CreateWalletTransaction;

public class CreateWalletTransactionCommandHandler : IRequestHandler<CreateWalletTransactionCommand, WalletTransactionDto>
{
    private readonly IWalletTransactionRepository _walletTransactionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateWalletTransactionCommandHandler> _logger;
    private readonly ICacheService _cacheService;

    public CreateWalletTransactionCommandHandler(
        IWalletTransactionRepository walletTransactionRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ILogger<CreateWalletTransactionCommandHandler> logger,
        ICacheService cacheService)
    {
        _walletTransactionRepository = walletTransactionRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _cacheService = cacheService;
    }

    public async Task<WalletTransactionDto> Handle(CreateWalletTransactionCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;
        WalletTransactionDto result = null!;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var user = await _userRepository.GetByIdAsync(dto.UserId) ?? throw new ArgumentException("User not found");

            if (dto.Type != WalletConstants.Credit && dto.Type != WalletConstants.Debit)
                throw new ArgumentException("Transaction type must be 'Credit' or 'Debit'");

            // Acquire advisory lock to prevent race conditions on the same userId
            await _walletTransactionRepository.AcquireUserWalletLockAsync(dto.UserId);

            // Re-read balance AFTER acquiring the lock — bypass cache for authoritative read
            var currentBalance = await _walletTransactionRepository.GetUserBalanceAsync(dto.UserId);

            // Validate wallet limit for Credit transactions
            if (dto.Type == WalletConstants.Credit)
            {
                var newBalance = currentBalance + dto.Amount;

                if (!dto.IsAdminCredit && dto.Amount < WalletConstants.MinTopUpAmount)
                    throw new InvalidOperationException($"Minimum top-up amount is ₹{WalletConstants.MinTopUpAmount}");

                if (newBalance > WalletConstants.MaxWalletBalance)
                {
                    var remaining = WalletConstants.MaxWalletBalance - currentBalance;
                    throw new InvalidOperationException(
                        $"Cannot add ₹{dto.Amount}. Maximum wallet balance is ₹{WalletConstants.MaxWalletBalance}. " +
                        $"Current balance: ₹{currentBalance}. You can add up to ₹{remaining}."
                    );
                }
            }

            // Validate sufficient balance for Debit transactions
            if (dto.Type == WalletConstants.Debit && currentBalance < dto.Amount)
            {
                _logger.LogWarning(
                    "Wallet debit failed - insufficient funds: UserId={UserId} Required={Required} Available={Available}",
                    dto.UserId, dto.Amount, currentBalance);

                throw new InsufficientBalanceException(dto.Amount, currentBalance);
            }

            var transaction = new WalletTransaction
            {
                UserId = dto.UserId,
                Amount = dto.Amount,
                Type = dto.Type,
                Description = dto.Description,
                ReferenceType = dto.ReferenceType,
                ReferenceId = dto.ReferenceId
            };

            var created = await _walletTransactionRepository.CreateAsync(transaction);
            var transactionFromDb = await _walletTransactionRepository.GetByIdAsync(created.TransactionId);
            if (transactionFromDb == null) throw new InvalidOperationException("Transaction creation failed.");

            result = WalletTransactionMapper.MapToDto(transactionFromDb);

            // LOG SUCCESS
            if (dto.Type == WalletConstants.Debit)
            {
                _logger.LogInformation(
                    "Wallet debit: UserId={UserId} Amount={Amount} OrderId={OrderId} NewBalance={NewBalance}",
                    dto.UserId, dto.Amount, dto.ScheduledOrderId, currentBalance - dto.Amount);
            }
            else
            {
                _logger.LogInformation(
                    "Wallet credit: UserId={UserId} Amount={Amount} Description={Description}",
                    dto.UserId, dto.Amount, dto.Description);
            }
        });

        // Invalidate cache after successful transaction commit
        await _cacheService.RemoveAsync(CacheKeys.WalletBalance(dto.UserId));

        return result;
    }
}
