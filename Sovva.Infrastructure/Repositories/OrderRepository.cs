using System.Threading.Tasks;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;
using Sovva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Sovva.Infrastructure.Repositories
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
            return await _context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderId == id);
        }

        public void Update(Order order)
        {
            _context.Orders.Update(order);
            // No SaveChanges here — caller decides when to commit
        }

        public async Task<IEnumerable<Order>> GetByUserIdAsync(int userId)
        {
            return await _context.Orders.AsNoTracking().Where(o => o.UserId == userId).ToListAsync();
        }

        public async Task<IEnumerable<Order>> GetByStatusAsync(OrderStatus status)
        {
            return await _context.Orders.AsNoTracking().Where(o => o.OrderStatus == status).ToListAsync();
        }

        // ✅ FIX 7: Added pagination to prevent unbounded queries
        public async Task<IEnumerable<Order>> GetAllAsync(int page = 1, int pageSize = 50)
        {
            return await _context.Orders.AsNoTracking()
                .OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        // ✅ NEW: Enhanced methods with eager loading for rich data
        public async Task<IEnumerable<Order>> GetUserOrdersWithDetailsAsync(int userId)
        {
            return await _context.Orders.AsNoTracking()
                .Include(o => o.UserMeal)
                    .ThenInclude(um => um!.Meal)
                .Include(o => o.UserMeal)
                    .ThenInclude(um => um!.UserMealIngredients)
                        .ThenInclude(umi => umi.Ingredient)
                // ✅ NEW: Include source scheduled order with ingredients
                .Include(o => o.SourceScheduledOrder)
                    .ThenInclude(so => so!.Ingredients)
                        .ThenInclude(i => i.Ingredient)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Order>> GetAllOrdersWithDetailsAsync()
        {
            return await _context.Orders.AsNoTracking()
                .Include(o => o.UserMeal)
                    .ThenInclude(um => um!.Meal)
                .Include(o => o.UserMeal)
                    .ThenInclude(um => um!.UserMealIngredients)
                        .ThenInclude(umi => umi.Ingredient)
                // ✅ NEW: Include source scheduled order with ingredients
                .Include(o => o.SourceScheduledOrder)
                    .ThenInclude(so => so!.Ingredients)
                        .ThenInclude(i => i.Ingredient)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        /// <summary>
        /// ✅ NEW: Get order by ScheduledOrderId for idempotency check
        /// Uses AsNoTracking to avoid EF Core tracking conflicts
        /// </summary>
        public async Task<Order?> GetByScheduledOrderIdAsync(int scheduledOrderId)
        {
            return await _context.Orders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.ScheduledOrderId == scheduledOrderId);
        }
    }
}
