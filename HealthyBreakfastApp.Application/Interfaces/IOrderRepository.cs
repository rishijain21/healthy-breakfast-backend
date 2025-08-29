using HealthyBreakfastApp.Domain.Entities;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IOrderRepository
    {
        Task AddAsync(Order entity);
        Task SaveChangesAsync();
        Task<Order?> GetByIdAsync(int id);
        
        // Add these missing methods:
        Task UpdateAsync(Order order);
        Task<IEnumerable<Order>> GetByUserIdAsync(int userId);
        Task<IEnumerable<Order>> GetByStatusAsync(string status);
    }
}
