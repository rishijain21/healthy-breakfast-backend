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
        
        // ✅ EXISTING: Keep for backward compatibility
        Task<IEnumerable<OrderDto>> GetUserOrdersAsync(int userId);
        Task<IEnumerable<OrderDto>> GetAllOrderHistoryAsync();

        // ✅ NEW: Enhanced methods with rich data
        Task<IEnumerable<EnhancedOrderHistoryDto>> GetUserOrdersWithDetailsAsync(int userId);
        Task<IEnumerable<EnhancedOrderHistoryDto>> GetAllOrderHistoryWithDetailsAsync();
    }
}
