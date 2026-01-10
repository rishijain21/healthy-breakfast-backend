using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthyBreakfastApp.Domain.Entities;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IKitchenRepository
    {
        Task<List<Order>> GetOrdersForPreparationAsync(DateTime date);
        Task<Order> GetOrderByIdAsync(int orderId);
        Task UpdateOrderAsync(Order order);
    }
}
