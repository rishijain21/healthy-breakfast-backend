using Sovva.Domain.Entities;
using Sovva.Domain.Enums;  // ✅ ADD this import
using System.Threading.Tasks;

namespace Sovva.Application.Interfaces
{
    public interface IOrderRepository
    {
        Task AddAsync(Order entity);
        Task SaveChangesAsync();
        Task<Order?> GetByIdAsync(long id);
        void Update(Order order);
        Task<IEnumerable<Order>> GetByUserIdAsync(int userId);
        Task<(IEnumerable<Order> Items, int TotalCount)> GetByUserIdPagedAsync(int userId, int page, int pageSize);
        
        // ✅ FIXED: Changed from string to OrderStatus enum + added pagination
        Task<IEnumerable<Order>> GetByStatusAsync(OrderStatus status, int page = 1, int pageSize = 50);
        
        // ✅ FIX 7: Added pagination parameters to prevent unbounded queries
        Task<IEnumerable<Order>> GetAllAsync(int page = 1, int pageSize = 50);

        // ✅ NEW: Enhanced methods with eager loading
        Task<IEnumerable<Order>> GetUserOrdersWithDetailsAsync(int userId);
        Task<(IEnumerable<Order> Items, int TotalCount)> GetUserOrdersWithDetailsPagedAsync(int userId, int page, int pageSize);
        Task<IEnumerable<Order>> GetAllOrdersWithDetailsAsync(int page = 1, int pageSize = 50);

        /// </summary>
        Task<Order?> GetByScheduledOrderIdAsync(int scheduledOrderId);

        // ✅ FIX 13: Batch idempotency check
        Task<Dictionary<int, Order>> GetByScheduledOrderIdsAsync(IEnumerable<int> scheduledOrderIds);

        /// <summary>
        /// ✅ NEW: Get recent order by UserMealId for duplicate prevention (C-1)
        /// </summary>
        Task<Order?> GetRecentOrderByUserMealIdAsync(int userMealId, int userId, int withinSeconds);

        // ✅ NEW: Count methods for pagination
        Task<int> CountAsync();
        Task<int> CountByStatusAsync(OrderStatus status);
    }
}
