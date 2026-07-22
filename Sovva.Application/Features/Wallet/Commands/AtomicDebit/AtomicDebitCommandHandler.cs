using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Sovva.Application.Common.Infrastructure;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;

namespace Sovva.Application.Features.Wallet.Commands.AtomicDebit;

public class AtomicDebitCommandHandler : IRequestHandler<AtomicDebitCommand, (bool Success, long? TransactionId)>
{
    private readonly IWalletTransactionRepository _walletTransactionRepository;
    private readonly IFailedOrderAttemptRepository _failedOrderAttemptRepository;
    private readonly ICacheService _cacheService;
    private readonly IAppTimeProvider _time;
    private readonly ILogger<AtomicDebitCommandHandler> _logger;

    public AtomicDebitCommandHandler(
        IWalletTransactionRepository walletTransactionRepository,
        IFailedOrderAttemptRepository failedOrderAttemptRepository,
        ICacheService cacheService,
        IAppTimeProvider time,
        ILogger<AtomicDebitCommandHandler> logger)
    {
        _walletTransactionRepository = walletTransactionRepository;
        _failedOrderAttemptRepository = failedOrderAttemptRepository;
        _cacheService = cacheService;
        _time = time;
        _logger = logger;
    }

    public async Task<(bool Success, long? TransactionId)> Handle(AtomicDebitCommand request, CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogDebug("AtomicDebitCommand called for UserId={UserId}, Amount={Amount}", request.UserId, request.Amount);

        var result = await _walletTransactionRepository.AtomicDebitAsync(
            request.UserId,
            request.Amount,
            request.Description,
            request.ScheduledOrderId);

        if (result.Success)
        {
            await _cacheService.RemoveAsync(CacheKeys.WalletBalance(request.UserId));

            _logger.LogInformation(
                "Wallet atomic debit: UserId={UserId} Amount={Amount} ScheduledOrderId={ScheduledOrderId} TransactionId={TransactionId}",
                request.UserId, request.Amount, request.ScheduledOrderId, result.TransactionId);
        }
        else
        {
            // Read authoritative balance directly from repo to avoid cache staleness during failure logging
            var currentBalance = await _walletTransactionRepository.GetUserBalanceAsync(request.UserId);
            _logger.LogWarning(
                "Wallet atomic debit FAILED (insufficient balance): UserId={UserId} Required={Amount} ScheduledOrderId={ScheduledOrderId} CurrentBalance={CurrentBalance}",
                request.UserId, request.Amount, request.ScheduledOrderId, currentBalance);

            if (request.ScheduledOrderId.HasValue)
            {
                await _failedOrderAttemptRepository.AddAsync(new FailedOrderAttempt
                {
                    UserId = request.UserId,
                    ScheduledOrderId = request.ScheduledOrderId.Value,
                    RequiredAmount = request.Amount,
                    AvailableBalance = currentBalance,
                    Reason = "Insufficient wallet balance",
                    AttemptedAt = _time.UtcNow
                });
            }
        }

        stopwatch.Stop();
        _logger.LogInformation("[METRICS] AtomicDebitCommand took {Ms}ms", stopwatch.ElapsedMilliseconds);

        return result;
    }
}
