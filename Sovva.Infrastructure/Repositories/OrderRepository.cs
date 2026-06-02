using System.Threading.Tasks;
using Sovva.Application.Interfaces;
using Sovva.Application.Helpers;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;
using Sovva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Sovva.Infrastructure.Repositories
{
    internal class OrderRepository : IOrderRepository
    {
        private readonly AppDbContext _context;
        private readonly IAppTimeProvider _time;

        public OrderRepository(AppDbContext context, IAppTimeProvider time)
        {
            _context = context;
            _time = time;
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

        public async Task<Order?> GetByIdAsync(long id)
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

        public async Task<IEnumerable<Order>> GetByStatusAsync(OrderStatus status, int page = 1, int pageSize = 50)
        {
            return await _context.Orders.AsNoTracking()
                .Where(o => o.OrderStatus == status)
                .OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
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
                .AsSplitQuery()
                .ToListAsync();
        }

        public async Task<IEnumerable<Order>> GetAllOrdersWithDetailsAsync(int page = 1, int pageSize = 50)
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
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .AsSplitQuery()
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

        // ✅ FIX 13: Batch idempotency check
        public async Task<Dictionary<int, Order>> GetByScheduledOrderIdsAsync(IEnumerable<int> scheduledOrderIds)
        {
            return await _context.Orders
                .AsNoTracking()
                .Where(o => o.ScheduledOrderId.HasValue && scheduledOrderIds.Contains(o.ScheduledOrderId.Value))
                .ToDictionaryAsync(o => o.ScheduledOrderId!.Value);
        }

        public async Task<Order?> GetRecentOrderByUserMealIdAsync(int userMealId, int userId, int withinSeconds)
        {
            return await _context.Orders
                .AsNoTracking()
                .Where(o => o.UserMealId == userMealId 
                    && o.UserId == userId
                    && o.CreatedAt >= _time.UtcNow.AddSeconds(-withinSeconds))
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();
        }
        public async Task<int> CountAsync()
        {
            return await _context.Orders.CountAsync();
        }

        public async Task<int> CountByStatusAsync(OrderStatus status)
        {
            return await _context.Orders.CountAsync(o => o.OrderStatus == status);
        }
    }
}
