using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.ScheduledOrders.Commands.ProcessSingleScheduledOrder
{
    public class ProcessSingleScheduledOrderCommandHandler : IRequestHandler<ProcessSingleScheduledOrderCommand, bool>
    {
        private readonly IScheduledOrderRepository _scheduledOrderRepository;
        private readonly IWalletTransactionService _walletService;
        private readonly IOrderService _orderService;
        private readonly IAppTimeProvider _time;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ProcessSingleScheduledOrderCommandHandler> _logger;

        public ProcessSingleScheduledOrderCommandHandler(
            IScheduledOrderRepository scheduledOrderRepository,
            IWalletTransactionService walletService,
            IOrderService orderService,
            IAppTimeProvider time,
            IUnitOfWork unitOfWork,
            ILogger<ProcessSingleScheduledOrderCommandHandler> logger)
        {
            _scheduledOrderRepository = scheduledOrderRepository;
            _walletService = walletService;
            _orderService = orderService;
            _time = time;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<bool> Handle(ProcessSingleScheduledOrderCommand request, CancellationToken cancellationToken)
        {
            return await ScheduledOrderHelper.ProcessSingleScheduledOrderAsync(
                request.ScheduledOrder,
                request.UsersByAuthId,
                request.ExistingOrders,
                request.ExistingTransactions,
                request.AddressesMap,
                _scheduledOrderRepository,
                _walletService,
                _orderService,
                _time,
                _unitOfWork,
                _logger
            );
        }
    }
}
