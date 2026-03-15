using Sovva.Domain.Entities;
using Sovva.Domain.Enums;  // ✅ ADD this import
using System.Threading.Tasks;

namespace Sovva.Application.Interfaces
{
    public interface IOrderRepository
    {
        Task AddAsync(Order entity);
        Task SaveChangesAsync();
        Task<Order?> GetByIdAsync(int id);
        void Update(Order order);
        Task<IEnumerable<Order>> GetByUserIdAsync(int userId);
        
        // ✅ FIXED: Changed from string to OrderStatus enum
        Task<IEnumerable<Order>> GetByStatusAsync(OrderStatus status);
        
        // ✅ FIX 7: Added pagination parameters to prevent unbounded queries
        Task<IEnumerable<Order>> GetAllAsync(int page = 1, int pageSize = 50);

        // ✅ NEW: Enhanced methods with eager loading
        Task<IEnumerable<Order>> GetUserOrdersWithDetailsAsync(int userId);
        Task<IEnumerable<Order>> GetAllOrdersWithDetailsAsync();
    }
}
