using Sovva.Application.DTOs;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;
using System.Threading.Tasks;

namespace Sovva.Application.Interfaces
{
    public interface IOrderService
    {
        // ✅ SECURE: Create order with userId from JWT token
        [Obsolete("Do not use. Relies on client-trusted TotalPrice. Use ConfirmScheduledOrderAsync or MealBuilder paths.")]
        Task<long> CreateOrderAsync(CreateOrderDto dto, int userId);
        Task<OrderDto?> GetOrderByIdAsync(long id);
        
        // ✅ NEW: Enhanced single order fetch
        Task<EnhancedOrderHistoryDto?> GetOrderDetailsByIdAsync(long id);
        
        // ✅ SECURE: Meal builder method with userId from JWT token
        Task<OrderCreationResponseDto> CreateOrderFromMealBuilderAsync(CreateOrderFromMealBuilderDto dto, int userId);
        
        // ✅ NEW: Meal builder method with explicit DeliveryAddressId (for scheduled order confirmation)
        Task<OrderCreationResponseDto> CreateOrderFromMealBuilderAsync(
            CreateOrderFromMealBuilderDto dto, 
            int userId, 
            int? deliveryAddressId);
        
        // ✅ EXISTING: Keep for backward compatibility
        Task<PagedResult<OrderDto>> GetUserOrdersAsync(int userId, int page = 1, int pageSize = 20);
        Task<PagedResult<OrderDto>> GetAllOrderHistoryAsync(int page = 1, int pageSize = 50);
        Task<PagedResult<OrderDto>> GetOrdersByStatusAsync(OrderStatus status, int page = 1, int pageSize = 50);

        // ✅ NEW: Enhanced methods with rich data
        Task<PagedResult<EnhancedOrderHistoryDto>> GetUserOrdersWithDetailsAsync(int userId, int page = 1, int pageSize = 20);
        Task<PagedResult<EnhancedOrderHistoryDto>> GetAllOrderHistoryWithDetailsAsync(int page = 1, int pageSize = 50);

        // ✅ NEW: Dedicated method for confirming scheduled orders (no catalogue lookup, no UserMeal creation)
        Task<int> ConfirmScheduledOrderAsync(ScheduledOrder scheduledOrder, Order? existingOrder = null);

        /// <summary>
        /// ✅ NEW: Get order by ScheduledOrderId for idempotency check
        /// Used by midnight job to check if order was already created
        /// </summary>
        Task<Order?> GetByScheduledOrderIdAsync(int scheduledOrderId);

        // ✅ NEW: Post-delivery actions
        Task<bool> RateOrderAsync(long orderId, int userId, int rating, string? review);
        Task<OrderCreationResponseDto> ReorderAsync(long orderId, int userId);
    }
}
