using HealthyBreakfastApp.Application.DTOs;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IOrderService
    {
        // ✅ SECURE: Create order with userId from JWT token
        Task<int> CreateOrderAsync(CreateOrderDto dto, int userId);
        Task<OrderDto?> GetOrderByIdAsync(int id);
        
        // ✅ SECURE: Meal builder method with userId from JWT token
        Task<OrderCreationResponseDto> CreateOrderFromMealBuilderAsync(CreateOrderFromMealBuilderDto dto, int userId);
        
        // ✅ NEW: Meal builder method with explicit DeliveryAddressId (for scheduled order confirmation)
        Task<OrderCreationResponseDto> CreateOrderFromMealBuilderAsync(
            CreateOrderFromMealBuilderDto dto, 
            int userId, 
            int? deliveryAddressId);
        
        // ✅ EXISTING: Keep for backward compatibility
        Task<IEnumerable<OrderDto>> GetUserOrdersAsync(int userId);
        Task<IEnumerable<OrderDto>> GetAllOrderHistoryAsync();

        // ✅ NEW: Enhanced methods with rich data
        Task<IEnumerable<EnhancedOrderHistoryDto>> GetUserOrdersWithDetailsAsync(int userId);
        Task<IEnumerable<EnhancedOrderHistoryDto>> GetAllOrderHistoryWithDetailsAsync();
    }
}
