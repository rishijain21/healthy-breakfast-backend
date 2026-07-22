using System;
using System.Collections.Generic;
using MediatR;
using Sovva.Domain.Entities;

namespace Sovva.Application.Features.ScheduledOrders.Commands.ProcessSingleScheduledOrder
{
    public record ProcessSingleScheduledOrderCommand(
        ScheduledOrder ScheduledOrder,
        IReadOnlyDictionary<Guid, User> UsersByAuthId,
        IReadOnlyDictionary<int, Order> ExistingOrders,
        IReadOnlyDictionary<int, WalletTransaction> ExistingTransactions,
        IReadOnlyDictionary<int, UserAddress> AddressesMap
    ) : IRequest<bool>;
}
