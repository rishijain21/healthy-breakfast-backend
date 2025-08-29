using HealthyBreakfastApp.Application.DTOs;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IOrderService
    {
        Task<int> CreateOrderAsync(CreateOrderDto dto);
        Task<OrderDto?> GetOrderByIdAsync(int id);
        
        // Add only the meal builder method for now
        Task<OrderCreationResponseDto> CreateOrderFromMealBuilderAsync(CreateOrderFromMealBuilderDto dto);
    }
}
