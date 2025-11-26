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
                        .ThenInclude(i => i.IngredientCategory)
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

        // ✅ FIXED: Reload entity to avoid tracking issues
        public async Task<ScheduledOrder> UpdateAsync(ScheduledOrder scheduledOrder)
        {
            // Find the existing entity in the database
            var existingOrder = await _context.ScheduledOrders
                .FirstOrDefaultAsync(so => so.ScheduledOrderId == scheduledOrder.ScheduledOrderId);
            
            if (existingOrder == null)
            {
                throw new InvalidOperationException($"Scheduled order {scheduledOrder.ScheduledOrderId} not found");
            }
            
            // Update properties
            existingOrder.OrderStatus = scheduledOrder.OrderStatus;
            existingOrder.CanModify = scheduledOrder.CanModify;
            existingOrder.ConfirmedAt = scheduledOrder.ConfirmedAt;
            existingOrder.TotalPrice = scheduledOrder.TotalPrice;
            existingOrder.DeliveryTimeSlot = scheduledOrder.DeliveryTimeSlot;
            existingOrder.NutritionalSummary = scheduledOrder.NutritionalSummary;
            existingOrder.UpdatedAt = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();
            return existingOrder;
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

        // ✅ FIXED: In-memory date filtering to avoid PostgreSQL date type issues
        public async Task<List<ScheduledOrder>> GetScheduledOrdersForDateAsync(DateTime date)
        {
            // Load all scheduled orders with status "scheduled"
            var scheduledOrders = await _context.ScheduledOrders
                .Include(so => so.Ingredients)
                    .ThenInclude(soi => soi.Ingredient)
                        .ThenInclude(i => i.IngredientCategory)
                .Include(so => so.User)
                .Where(so => so.OrderStatus == "scheduled")
                .ToListAsync();
            
            // Filter by date in memory (avoids EF/PostgreSQL date type issues)
            var targetDate = date.Date;
            return scheduledOrders
                .Where(so => so.ScheduledFor.Date == targetDate)
                .ToList();
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
