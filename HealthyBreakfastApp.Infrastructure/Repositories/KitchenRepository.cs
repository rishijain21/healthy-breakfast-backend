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
            return await _context.Orders
                .Include(o => o.User)
                .Include(o => o.UserMeal)
                    .ThenInclude(um => um.UserMealIngredients)
                    .ThenInclude(umi => umi.Ingredient)
                        .ThenInclude(i => i.IngredientCategory)
                .Where(o => o.ScheduledFor.Date == date.Date && !o.IsPrepared)
                .OrderBy(o => o.CreatedAt)
                .ToListAsync();
        }

        public async Task<Order> GetOrderByIdAsync(int orderId)
        {
            return await _context.Orders
                .Include(o => o.User)
                .Include(o => o.UserMeal)
                    .ThenInclude(um => um.UserMealIngredients)
                    .ThenInclude(umi => umi.Ingredient)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);
        }

        public async Task UpdateOrderAsync(Order order)
        {
            _context.Orders.Update(order);
            await _context.SaveChangesAsync();
        }
    }
}
