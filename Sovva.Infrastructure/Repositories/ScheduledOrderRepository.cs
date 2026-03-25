using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;
using Sovva.Infrastructure.Data;

namespace Sovva.Infrastructure.Repositories
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

        // ─────────────────────────────────────────────────────────────────────
        // CREATE
        // ─────────────────────────────────────────────────────────────────────

        public async Task<ScheduledOrder> CreateAsync(ScheduledOrder scheduledOrder)
        {
            _context.ScheduledOrders.Add(scheduledOrder);
            await _context.SaveChangesAsync();
            return scheduledOrder;
        }

        // ─────────────────────────────────────────────────────────────────────
        // READ — user-scoped
        // ─────────────────────────────────────────────────────────────────────

        public async Task<List<ScheduledOrder>> GetByAuthIdAndDateAsync(Guid authId, DateTime date)
        {
            // date is treated as an IST calendar date (the caller passes IST date)
            // ScheduledFor is stored as DateTime with 00:00:00 time portion
            var targetDate = date.Date;
            var targetDateEnd = targetDate.AddDays(1);

            return await _context.ScheduledOrders
                .AsNoTracking()
                .Include(so => so.Ingredients)
                    .ThenInclude(soi => soi.Ingredient)
                        .ThenInclude(i => i.IngredientCategory)
                .Where(so => so.AuthId == authId
                          && so.ScheduledFor >= targetDate
                          && so.ScheduledFor < targetDateEnd)
                .OrderBy(so => so.CreatedAt)
                .ToListAsync();
        }

        /// <summary>
        /// Get scheduled orders by userId (internal database ID) for a specific date
        /// </summary>
        public async Task<List<ScheduledOrder>> GetByUserIdAndDateAsync(int userId, DateTime date)
        {
            var targetDate = date.Date;
            var targetDateEnd = targetDate.AddDays(1);

            return await _context.ScheduledOrders
                .AsNoTracking()
                .Include(so => so.Ingredients)
                    .ThenInclude(soi => soi.Ingredient)
                        .ThenInclude(i => i.IngredientCategory)
                .Where(so => so.UserId == userId
                          && so.ScheduledFor >= targetDate
                          && so.ScheduledFor < targetDateEnd)
                .OrderBy(so => so.CreatedAt)
                .ToListAsync();
        }

        public async Task<ScheduledOrder?> GetByIdAndAuthIdAsync(int scheduledOrderId, Guid authId)
        {
            return await _context.ScheduledOrders
                .AsNoTracking()
                .Include(so => so.Ingredients)
                    .ThenInclude(soi => soi.Ingredient)
                .FirstOrDefaultAsync(so => so.ScheduledOrderId == scheduledOrderId
                                        && so.AuthId == authId);
        }

        // ─────────────────────────────────────────────────────────────────────
        // READ — job-scoped
        //
        // FIX 1: ScheduledFor is a DATE column storing the IST calendar date.
        //         We receive a UTC range from the job, convert to IST date,
        //         then do a simple date equality query — clean and sargable.
        // ─────────────────────────────────────────────────────────────────────

        public async Task<List<ScheduledOrder>> GetScheduledOrdersForUtcRangeAsync(
            DateTime startUtc, DateTime endUtc)
        {
            // Convert UTC lower-bound to IST to get the delivery calendar date
            var istDate = TimeZoneHelper.ToIST(startUtc).Date;
            
            // Convert to DateTime range for comparison (ScheduledFor is DateTime with 00:00:00)
            var targetDateStart = istDate;
            var targetDateEnd = istDate.AddDays(1);

            _logger.LogInformation(
                "📅 [Repo] Querying ScheduledFor >= {Date} AND < {DateEnd} (IST calendar date, UTC range {Start}→{End})",
                targetDateStart, targetDateEnd, startUtc, endUtc);

            var orders = await _context.ScheduledOrders
                .AsNoTracking()
                .Where(so => so.ScheduledFor >= targetDateStart && so.ScheduledFor < targetDateEnd)
                .Include(o => o.User)
                .Include(o => o.Ingredients)
                    .ThenInclude(i => i.Ingredient)
                        .ThenInclude(ing => ing.IngredientCategory)
                .Include(o => o.DeliveryAddress)
                    .ThenInclude(a => a!.ServiceableLocation)
                .OrderBy(o => o.CreatedAt)
                .ToListAsync();

            _logger.LogInformation("📦 [Repo] Found {Count} orders for {Date}", orders.Count, istDate);
            return orders;
        }

        // Legacy method — kept for backward compatibility with controller manual endpoints.
        // Uses date range comparison since ScheduledFor is DateTime
        public async Task<List<ScheduledOrder>> GetScheduledOrdersForDateAsync(DateTime date)
        {
            // Treat the incoming DateTime as already an IST date (how callers pass it)
            var targetDateStart = date.Date;
            var targetDateEnd = targetDateStart.AddDays(1);

            _logger.LogDebug("📅 [Repo] GetScheduledOrdersForDateAsync — date >= {Start} AND < {End}", 
                targetDateStart, targetDateEnd);

            return await _context.ScheduledOrders
                .AsNoTracking()
                .Where(so => so.ScheduledFor >= targetDateStart && so.ScheduledFor < targetDateEnd)
                .Include(o => o.User)
                .Include(o => o.Ingredients)
                    .ThenInclude(i => i.Ingredient)
                        .ThenInclude(ing => ing.IngredientCategory)
                .Include(o => o.DeliveryAddress)
                    .ThenInclude(a => a!.ServiceableLocation)
                .OrderBy(o => o.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> HasScheduledOrdersForDateAsync(Guid authId, DateTime date)
        {
            var targetDateStart = date.Date;
            var targetDateEnd = targetDateStart.AddDays(1);
            
            return await _context.ScheduledOrders
                .AsNoTracking()
                .AnyAsync(so => so.AuthId == authId
                              && so.ScheduledFor >= targetDateStart
                              && so.ScheduledFor < targetDateEnd
                              && so.OrderStatus == "scheduled");
        }

        public async Task<List<ScheduledOrder>> GetBySubscriptionIdAsync(int subscriptionId)
        {
            return await _context.ScheduledOrders
                .AsNoTracking()
                .Where(so => so.SubscriptionId == subscriptionId)
                .ToListAsync();
        }

        /// <summary>
        /// ✅ NEW: Check if a scheduled order already exists for a subscription on a specific date
        /// Used to prevent duplicate order generation on job retry
        /// </summary>
        public async Task<ScheduledOrder?> GetBySubscriptionIdAndDateAsync(int subscriptionId, DateOnly date)
        {
            // Convert DateOnly to DateTime for comparison with ScheduledFor column
            var targetDate = date.ToDateTime(TimeOnly.MinValue);
            var targetDateEnd = targetDate.AddDays(1);

            return await _context.ScheduledOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(so => 
                    so.SubscriptionId == subscriptionId &&
                    so.ScheduledFor >= targetDate &&
                    so.ScheduledFor < targetDateEnd);
        }

        // ─────────────────────────────────────────────────────────────────────
        // UPDATE
        //
        // FIX 2: Also updates IsProcessedToOrder and ConfirmedOrderId so we have
        //         a proper audit trail linking scheduled → confirmed order.
        // ─────────────────────────────────────────────────────────────────────

        public async Task<ScheduledOrder> UpdateAsync(ScheduledOrder scheduledOrder)
        {
            var existing = await _context.ScheduledOrders
                .FirstOrDefaultAsync(so => so.ScheduledOrderId == scheduledOrder.ScheduledOrderId);

            if (existing == null)
                throw new InvalidOperationException(
                    $"ScheduledOrder #{scheduledOrder.ScheduledOrderId} not found for update");

            existing.OrderStatus        = scheduledOrder.OrderStatus;
            existing.CanModify          = scheduledOrder.CanModify;
            existing.ConfirmedAt        = scheduledOrder.ConfirmedAt;
            existing.TotalPrice         = scheduledOrder.TotalPrice;
            existing.DeliveryTimeSlot   = scheduledOrder.DeliveryTimeSlot;
            existing.NutritionalSummary = scheduledOrder.NutritionalSummary;
            existing.UpdatedAt          = DateTime.UtcNow;

            // ✅ FIX: Populate audit fields when order is confirmed
            if (scheduledOrder.IsProcessedToOrder)
            {
                existing.IsProcessedToOrder = true;
                existing.ConfirmedOrderId   = scheduledOrder.ConfirmedOrderId;
            }

            await _context.SaveChangesAsync();
            return existing;
        }

        // ─────────────────────────────────────────────────────────────────────
        // DELETE
        // ─────────────────────────────────────────────────────────────────────

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
    }
}
