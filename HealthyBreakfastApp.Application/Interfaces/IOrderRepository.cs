using HealthyBreakfastApp.Domain.Entities;
using HealthyBreakfastApp.Domain.Enums;  // ✅ ADD this import
using System.Threading.Tasks;

namespace HealthyBreakfastApp.Application.Interfaces
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
        
        Task<IEnumerable<Order>> GetAllAsync();

        // ✅ NEW: Enhanced methods with eager loading
        Task<IEnumerable<Order>> GetUserOrdersWithDetailsAsync(int userId);
        Task<IEnumerable<Order>> GetAllOrdersWithDetailsAsync();
    }
}
