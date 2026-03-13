using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Domain.Entities;
using HealthyBreakfastApp.Infrastructure.Data;

namespace HealthyBreakfastApp.Infrastructure.Repositories
{
    public class ScheduledOrderRepository : IScheduledOrderRepository
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ScheduledOrderRepository> _logger;

        public ScheduledOrderRepository(AppDbContext context, ILogger<ScheduledOrderRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ScheduledOrder> CreateAsync(ScheduledOrder scheduledOrder)
        {
            _context.ScheduledOrders.Add(scheduledOrder);
            await _context.SaveChangesAsync();
            return scheduledOrder;
        }

        public async Task<List<ScheduledOrder>> GetByAuthIdAndDateAsync(Guid authId, DateTime date)
        {
            // ✅ Range comparison — sargable, can use B-tree index on ScheduledFor
            var startOfDay = date.Date;
            var endOfDay = date.Date.AddDays(1);

            return await _context.ScheduledOrders
                .AsNoTracking()
                .Include(so => so.Ingredients)
                    .ThenInclude(soi => soi.Ingredient)
                        .ThenInclude(i => i.IngredientCategory)
                .Where(so => so.AuthId == authId
                          && so.ScheduledFor >= startOfDay
                          && so.ScheduledFor < endOfDay)
                .OrderBy(so => so.CreatedAt)
                .ToListAsync();
        }

        public async Task<ScheduledOrder?> GetByIdAndAuthIdAsync(int scheduledOrderId, Guid authId)
        {
            return await _context.ScheduledOrders
                .AsNoTracking()
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

        // ✅ FIXED: Use date range for sargable query
        public async Task<List<ScheduledOrder>> GetScheduledOrdersForDateAsync(DateTime date)
        {
            // Use date range comparison - sargable, can use B-tree index on ScheduledFor
            var startOfDay = date.Date;
            var endOfDay = date.Date.AddDays(1);
            
            // ✅ Replace Console.WriteLine with structured logging
            _logger.LogDebug("Searching for ScheduledFor >= {Start} AND < {End}", startOfDay, endOfDay);
            
            var orders = await _context.ScheduledOrders
                .AsNoTracking()
                .Where(so => so.ScheduledFor >= startOfDay && so.ScheduledFor < endOfDay)
                .Include(o => o.User)
                .Include(o => o.Ingredients)
                    .ThenInclude(i => i.Ingredient)
                        .ThenInclude(ing => ing.IngredientCategory)
                .Include(o => o.DeliveryAddress)
                    .ThenInclude(a => a.ServiceableLocation)
                .OrderBy(o => o.CreatedAt)
                .ToListAsync();
            
            _logger.LogDebug("Found {Count} orders for {Date:yyyy-MM-dd}", orders.Count, date);
            
            foreach (var order in orders)
            {
                _logger.LogTrace("   - Order #{OrderId}: {MealName}, ScheduledFor: {ScheduledFor:yyyy-MM-dd HH:mm:ss}, Status: {Status}",
                    order.ScheduledOrderId, order.MealName, order.ScheduledFor, order.OrderStatus);
            }
            
            return orders;
        }

        public async Task<bool> HasScheduledOrdersForDateAsync(Guid authId, DateTime date)
        {
            var startOfDay = date.Date;
            var endOfDay = date.Date.AddDays(1);

            return await _context.ScheduledOrders
                .AsNoTracking()
                .AnyAsync(so => so.AuthId == authId
                              && so.ScheduledFor >= startOfDay
                              && so.ScheduledFor < endOfDay
                              && so.OrderStatus == "scheduled");
        }

        public async Task<List<ScheduledOrder>> GetBySubscriptionIdAsync(int subscriptionId)
        {
            return await _context.ScheduledOrders
                .AsNoTracking()
                .Where(so => so.SubscriptionId == subscriptionId)
                .ToListAsync();
        }
    }
}
