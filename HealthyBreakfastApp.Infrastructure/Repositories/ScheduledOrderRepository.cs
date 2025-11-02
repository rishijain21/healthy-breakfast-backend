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
    public class ScheduledOrderRepository : IScheduledOrderRepository
    {
        private readonly AppDbContext _context;

        public ScheduledOrderRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ScheduledOrder> CreateAsync(ScheduledOrder scheduledOrder)
        {
            _context.ScheduledOrders.Add(scheduledOrder);
            await _context.SaveChangesAsync();
            return scheduledOrder;
        }

        public async Task<List<ScheduledOrder>> GetByAuthIdAndDateAsync(Guid authId, DateTime date)
        {
            return await _context.ScheduledOrders
                .Include(so => so.Ingredients)
                    .ThenInclude(soi => soi.Ingredient)
                      .ThenInclude(i => i.IngredientCategory) // ✅ CORRECT

                .Where(so => so.AuthId == authId && so.ScheduledFor.Date == date.Date)
                .OrderBy(so => so.CreatedAt)
                .ToListAsync();
        }

        public async Task<ScheduledOrder?> GetByIdAndAuthIdAsync(int scheduledOrderId, Guid authId)
        {
            return await _context.ScheduledOrders
                .Include(so => so.Ingredients)
                    .ThenInclude(soi => soi.Ingredient)
                .FirstOrDefaultAsync(so => so.ScheduledOrderId == scheduledOrderId && so.AuthId == authId);
        }

        public async Task<ScheduledOrder> UpdateAsync(ScheduledOrder scheduledOrder)
        {
            scheduledOrder.UpdatedAt = DateTime.UtcNow;
            _context.ScheduledOrders.Update(scheduledOrder);
            await _context.SaveChangesAsync();
            return scheduledOrder;
        }

        public async Task DeleteAsync(int scheduledOrderId)
        {
            var scheduledOrder = await _context.ScheduledOrders
                .Include(so => so.Ingredients)
                .FirstOrDefaultAsync(so => so.ScheduledOrderId == scheduledOrderId);
            
            if (scheduledOrder != null)
            {
                _context.ScheduledOrderIngredients.RemoveRange(scheduledOrder.Ingredients);
                _context.ScheduledOrders.Remove(scheduledOrder);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<ScheduledOrder>> GetScheduledOrdersForDateAsync(DateTime date)
        {
            return await _context.ScheduledOrders
                .Include(so => so.User)
                .Include(so => so.Ingredients)
                    .ThenInclude(soi => soi.Ingredient)
                .Where(so => so.ScheduledFor.Date == date.Date && 
                           so.OrderStatus == "scheduled" && 
                           so.ExpiresAt <= DateTime.UtcNow)
                .ToListAsync();
        }

        public async Task<bool> HasScheduledOrdersForDateAsync(Guid authId, DateTime date)
        {
            return await _context.ScheduledOrders
                .AnyAsync(so => so.AuthId == authId && 
                              so.ScheduledFor.Date == date.Date && 
                              so.OrderStatus == "scheduled");
        }
    }
}
