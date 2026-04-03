using Sovva.Application.DTOs;
using Sovva.Domain.Entities;
using System.Threading.Tasks;

namespace Sovva.Application.Interfaces
{
    public interface IOrderService
    {
        // ✅ SECURE: Create order with userId from JWT token
        Task<long> CreateOrderAsync(CreateOrderDto dto, int userId);
        Task<OrderDto?> GetOrderByIdAsync(long id);
        
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

        // ✅ NEW: Dedicated method for confirming scheduled orders (no catalogue lookup, no UserMeal creation)
        Task<int> ConfirmScheduledOrderAsync(ScheduledOrder scheduledOrder);

        /// <summary>
        /// ✅ NEW: Get order by ScheduledOrderId for idempotency check
        /// Used by midnight job to check if order was already created
        /// </summary>
        Task<Order?> GetByScheduledOrderIdAsync(int scheduledOrderId);
    }
}
