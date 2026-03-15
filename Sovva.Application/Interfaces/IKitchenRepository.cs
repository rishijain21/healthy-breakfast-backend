using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sovva.Domain.Entities;

namespace Sovva.Application.Interfaces
{
    public interface IKitchenRepository
    {
        Task<List<Order>> GetOrdersForPreparationAsync(DateTime date);
        Task<Order?> GetOrderByIdAsync(int orderId);
        Task UpdateOrderAsync(Order order);
    }
}
