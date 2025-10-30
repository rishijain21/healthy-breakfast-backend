using System.Threading.Tasks;
using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Domain.Entities;
using HealthyBreakfastApp.Domain.Enums;
using HealthyBreakfastApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HealthyBreakfastApp.Infrastructure.Repositories
{
    public class OrderRepository : IOrderRepository
    {
        private readonly AppDbContext _context;

        public OrderRepository(AppDbContext context)
        {
            _context = context;
        }

        // ✅ EXISTING: Keep all existing methods
        public async Task AddAsync(Order entity)
        {
            await _context.Orders.AddAsync(entity);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        public async Task<Order?> GetByIdAsync(int id)
        {
            return await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == id);
        }

        public async Task UpdateAsync(Order order)
        {
            _context.Orders.Update(order);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<Order>> GetByUserIdAsync(int userId)
        {
            return await _context.Orders.Where(o => o.UserId == userId).ToListAsync();
        }

        public async Task<IEnumerable<Order>> GetByStatusAsync(OrderStatus status)
        {
            return await _context.Orders.Where(o => o.OrderStatus == status).ToListAsync();
        }

        public async Task<IEnumerable<Order>> GetAllAsync()
        {
            return await _context.Orders
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        // ✅ NEW: Enhanced methods with eager loading for rich data
        public async Task<IEnumerable<Order>> GetUserOrdersWithDetailsAsync(int userId)
        {
            return await _context.Orders
                .Include(o => o.UserMeal)
                    .ThenInclude(um => um!.Meal)
                .Include(o => o.UserMeal)
                    .ThenInclude(um => um!.UserMealIngredients)
                        .ThenInclude(umi => umi.Ingredient)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Order>> GetAllOrdersWithDetailsAsync()
        {
            return await _context.Orders
                .Include(o => o.UserMeal)
                    .ThenInclude(um => um!.Meal)
                .Include(o => o.UserMeal)
                    .ThenInclude(um => um!.UserMealIngredients)
                        .ThenInclude(umi => umi.Ingredient)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }
    }
}
