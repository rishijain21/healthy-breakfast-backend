using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Domain.Entities;
using HealthyBreakfastApp.Infrastructure.Data;

namespace HealthyBreakfastApp.Infrastructure.Repositories
{
    public class KitchenRepository : IKitchenRepository
    {
        private readonly AppDbContext _context;

        public KitchenRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Order>> GetOrdersForPreparationAsync(DateTime date)
        {
            // Normalize the input date to date-only
            var targetDate = date.Date;
            
            Console.WriteLine($"🔍 [KitchenRepository] Querying Orders WHERE ScheduledFor.Date = {targetDate:yyyy-MM-dd}");
            Console.WriteLine($"🔍 [KitchenRepository] Input date kind: {date.Kind}, Target date: {targetDate:yyyy-MM-dd}");
            
            var orders = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.UserMeal)
                    .ThenInclude(um => um.UserMealIngredients)
                    .ThenInclude(umi => umi.Ingredient)
                        .ThenInclude(i => i.IngredientCategory)
                // ✅ NEW: Include DeliveryAddress and ServiceableLocation for batch grouping
                .Include(o => o.DeliveryAddress)
                    .ThenInclude(da => da.ServiceableLocation)
                .Where(o => o.ScheduledFor.Date == targetDate && !o.IsPrepared)
                .OrderBy(o => o.CreatedAt)
                .ToListAsync();
            
            Console.WriteLine($"📦 [KitchenRepository] Found {orders.Count} orders for {targetDate:yyyy-MM-dd}");
            
            foreach (var order in orders)
            {
                Console.WriteLine($"   - Order #{order.OrderId}: {order.UserMeal?.MealName ?? "Custom"}, ScheduledFor: {order.ScheduledFor:yyyy-MM-dd HH:mm:ss}, IsPrepared: {order.IsPrepared}");
            }
            
            return orders;
        }

        public async Task<Order> GetOrderByIdAsync(int orderId)
        {
            return await _context.Orders
                .Include(o => o.User)
                .Include(o => o.UserMeal)
                    .ThenInclude(um => um.UserMealIngredients)
                    .ThenInclude(umi => umi.Ingredient)
                // ✅ NEW: Include DeliveryAddress for order details
                .Include(o => o.DeliveryAddress)
                    .ThenInclude(da => da.ServiceableLocation)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);
        }

        public async Task UpdateOrderAsync(Order order)
        {
            _context.Orders.Update(order);
            await _context.SaveChangesAsync();
        }
    }
}
