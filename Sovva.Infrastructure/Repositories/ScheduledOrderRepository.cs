using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;
using Sovva.Infrastructure.Data;

namespace Sovva.Infrastructure.Repositories
{
    internal class ScheduledOrderRepository : IScheduledOrderRepository
    {
        private readonly AppDbContext _context;
        private readonly IAppTimeProvider _time;
        private readonly ILogger<ScheduledOrderRepository> _logger;

        public ScheduledOrderRepository(AppDbContext context, IAppTimeProvider time, ILogger<ScheduledOrderRepository> logger)
        {
            _context = context;
            _time = time;
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

        public async Task CreateBatchAsync(IEnumerable<ScheduledOrder> scheduledOrders)
        {
            await _context.ScheduledOrders.AddRangeAsync(scheduledOrders);
            await _context.SaveChangesAsync();
        }

        // ─────────────────────────────────────────────────────────────────────
        // READ — user-scoped
        // ─────────────────────────────────────────────────────────────────────

        public async Task<List<ScheduledOrder>> GetByAuthIdAndDateAsync(Guid authId, DateTime date)
        {
            // Convert to DateOnly for direct comparison with DATE column
            var istDateOnly = DateOnly.FromDateTime(
                date.Kind == DateTimeKind.Utc 
                    ? _time.ToIst(date) 
                    : date);

            return await _context.ScheduledOrders
                .AsNoTracking()
                .AsSplitQuery()
                .Include(so => so.Ingredients)
                    .ThenInclude(soi => soi.Ingredient)
                        .ThenInclude(i => i.IngredientCategory)
                .Where(so => so.AuthId == authId
                          && so.ScheduledFor == istDateOnly)
                .OrderBy(so => so.CreatedAt)
                .ToListAsync();
        }

        /// <summary>
        /// Get scheduled orders by userId (internal database ID) for a specific date
        /// </summary>
        public async Task<List<ScheduledOrder>> GetByUserIdAndDateAsync(int userId, DateTime date)
        {
            // Convert to DateOnly for direct comparison with DATE column
            var istDateOnly = DateOnly.FromDateTime(
                date.Kind == DateTimeKind.Utc 
                    ? _time.ToIst(date) 
                    : date);

            return await _context.ScheduledOrders
                .AsNoTracking()
                .AsSplitQuery()
                .Include(so => so.Ingredients)
                    .ThenInclude(soi => soi.Ingredient)
                        .ThenInclude(i => i.IngredientCategory)
                .Where(so => so.UserId == userId
                          && so.ScheduledFor == istDateOnly)
                .OrderBy(so => so.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<ScheduledOrder>> GetByUserIdAndDateRangeAsync(int userId, DateOnly from, DateOnly to)
        {
            return await _context.ScheduledOrders
                .AsNoTracking()
                .AsSplitQuery()
                .Include(so => so.Ingredients)
                    .ThenInclude(soi => soi.Ingredient)
                .Where(so => so.UserId == userId
                          && so.ScheduledFor >= from
                          && so.ScheduledFor <= to
                          && so.OrderStatus != ScheduledOrderStatus.Failed
                          && so.DeletedAt == null)
                .OrderBy(so => so.ScheduledFor)
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
        // FIX: ScheduledFor is a PostgreSQL DATE column storing the IST calendar date.
        //      We receive a UTC range from the job, convert to IST DateOnly,
        //      then do a simple DateOnly equality query — clean and type-safe.
        // ─────────────────────────────────────────────────────────────────────

        public async Task<List<ScheduledOrder>> GetScheduledOrdersForUtcRangeAsync(
            DateTime startUtc, DateTime endUtc)
        {
            // Convert UTC to IST DateOnly (the delivery calendar date)
            var istDate = DateOnly.FromDateTime(_time.ToIst(startUtc));
            
            _logger.LogInformation(
                "[Repo] Querying ScheduledFor = {Date} (IST). UTC range was {Start:u}→{End:u}",
                istDate, startUtc, endUtc);

            var orders = await _context.ScheduledOrders
                .AsNoTracking()
                .Where(so => so.ScheduledFor == istDate)           // ← DateOnly == DateOnly
                .Include(o => o.User)
                .Include(o => o.Ingredients)
                    .ThenInclude(i => i.Ingredient)
                        .ThenInclude(ing => ing.IngredientCategory)
                .Include(o => o.DeliveryAddress)
                    .ThenInclude(a => a!.ServiceableLocation)
                .OrderBy(o => o.CreatedAt)
                .ToListAsync();

            _logger.LogInformation("[Repo] Found {Count} orders for {Date}", orders.Count, istDate);
            return orders;
        }

        // Legacy method — kept for backward compatibility with controller manual endpoints.
        // Uses DateOnly equality since ScheduledFor is now DateOnly
        public async Task<List<ScheduledOrder>> GetScheduledOrdersForDateAsync(DateTime date)
        {
            // Convert to DateOnly for direct comparison
            var targetDate = DateOnly.FromDateTime(date.Date);

            _logger.LogDebug("[Repo] GetScheduledOrdersForDateAsync — ScheduledFor = {Date}", targetDate);

            return await _context.ScheduledOrders
                .AsNoTracking()
                .Where(so => so.ScheduledFor == targetDate)
                .Include(o => o.User)
                .Include(o => o.Ingredients)
                    .ThenInclude(i => i.Ingredient)
                        .ThenInclude(ing => ing.IngredientCategory)
                .Include(o => o.DeliveryAddress)
                    .ThenInclude(a => a!.ServiceableLocation)
                .OrderBy(o => o.CreatedAt)
                .ToListAsync();
        }

        // ✅ NEW: Clean DateOnly query for midnight job - queries unprocessed orders directly
        public async Task<List<ScheduledOrder>> GetScheduledOrdersForDateAsync(DateOnly date)
        {
            _logger.LogInformation("🔍 Querying scheduled orders for {Date:yyyy-MM-dd}", date);

            return await _context.ScheduledOrders
                .AsNoTracking()
                .Include(so => so.Ingredients)
                    .ThenInclude(soi => soi.Ingredient)
                .Where(so => so.ScheduledFor == date  // DateOnly == DateOnly ✅ clean
                          && !so.IsProcessedToOrder)
                .OrderBy(so => so.ScheduledOrderId)
                .ToListAsync();
        }

        public async Task<List<ScheduledOrder>> GetFailedScheduledOrdersAsync(DateOnly? targetDate)
        {
            var query = _context.ScheduledOrders
                .AsNoTracking()
                .Where(so => so.OrderStatus == ScheduledOrderStatus.Failed)
                .Include(o => o.User)
                .Include(o => o.Ingredients)
                    .ThenInclude(i => i.Ingredient)
                        .ThenInclude(ing => ing.IngredientCategory)
                .Include(o => o.DeliveryAddress)
                    .ThenInclude(a => a!.ServiceableLocation)
                .AsQueryable();

            if (targetDate.HasValue)
            {
                query = query.Where(so => so.ScheduledFor == targetDate.Value);
            }

            return await query.OrderBy(o => o.CreatedAt).ToListAsync();
        }

        public async Task<bool> HasScheduledOrdersForDateAsync(Guid authId, DateTime date)
        {
            // Convert to DateOnly for direct comparison with DATE column
            var targetDate = DateOnly.FromDateTime(date.Date);
            
            return await _context.ScheduledOrders
                .AsNoTracking()
                .AnyAsync(so => so.AuthId == authId
                              && so.ScheduledFor == targetDate
                              && so.OrderStatus == ScheduledOrderStatus.Scheduled);
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
            // DateOnly equality - direct comparison with DATE column
            return await _context.ScheduledOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(so => 
                    so.SubscriptionId == subscriptionId &&
                    so.ScheduledFor == date &&
                    so.OrderStatus != ScheduledOrderStatus.Failed);  // ← allow retry on failed orders
        }

        public async Task<List<int>> GetExistingSubscriptionOrdersForDateAsync(List<int> subscriptionIds, DateOnly date)
        {
            return await _context.ScheduledOrders
                .AsNoTracking()
                .Where(so => 
                    so.SubscriptionId.HasValue && 
                    subscriptionIds.Contains(so.SubscriptionId.Value) &&
                    so.ScheduledFor == date &&
                    so.OrderStatus != ScheduledOrderStatus.Failed)
                .Select(so => so.SubscriptionId!.Value)
                .ToListAsync();
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
                .Include(so => so.Ingredients)
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
            // UpdatedAt handled by TimestampInterceptor

            // ✅ FIX 6: Replace ingredients to prevent silent data loss
            if (scheduledOrder.Ingredients != null)
            {
                existing.Ingredients ??= new List<ScheduledOrderIngredient>();
                
                // Clear existing ingredients (EF will track as deleted)
                existing.Ingredients.Clear();
                
                // Add new ingredients
                foreach (var ing in scheduledOrder.Ingredients)
                {
                    existing.Ingredients.Add(ing);
                }
            }

            // ✅ FIX: Populate audit fields when order is confirmed
            if (scheduledOrder.IsProcessedToOrder)
            {
                existing.IsProcessedToOrder = true;
                existing.ConfirmedOrderId   = scheduledOrder.ConfirmedOrderId;
            }

            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task UpdateBatchAsync(IEnumerable<ScheduledOrder> scheduledOrders)
        {
            _context.ScheduledOrders.UpdateRange(scheduledOrders);
            await _context.SaveChangesAsync();
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
                scheduledOrder.DeletedAt = _time.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeleteBatchAsync(IEnumerable<int> scheduledOrderIds)
        {
            var orders = await _context.ScheduledOrders
                .Include(so => so.Ingredients)
                .Where(so => scheduledOrderIds.Contains(so.ScheduledOrderId))
                .ToListAsync();

            if (orders.Any())
            {
                var now = _time.UtcNow;
                foreach (var order in orders)
                {
                    order.DeletedAt = now;
                }
                await _context.SaveChangesAsync();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // ✅ NEW: Raw SQL methods for midnight job (avoid EF Core tracking)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Mark a scheduled order status using raw SQL (no EF tracker)
        /// Used by midnight job to update status without triggering entity tracking issues
        /// </summary>
        public async Task MarkAsAsync(int scheduledOrderId, string status)
        {
            await _context.Database.ExecuteSqlRawAsync(
                @"UPDATE public.""ScheduledOrders"" 
                  SET ""OrderStatus"" = @p0, 
                      ""CanModify"" = false, 
                      ""UpdatedAt"" = NOW() AT TIME ZONE 'UTC'
                  WHERE ""ScheduledOrderId"" = @p1",
                status, scheduledOrderId);
        }

        /// <summary>
        /// Mark a scheduled order as processed with confirmed order details
        /// Uses raw SQL to avoid EF Core tracking conflicts
        /// </summary>
        public async Task MarkAsProcessedAsync(
            int scheduledOrderId, 
            int confirmedOrderId, 
            DateTime confirmedAt)
        {
            await _context.Database.ExecuteSqlRawAsync(
                @"UPDATE public.""ScheduledOrders"" 
                  SET ""OrderStatus"" = 'Processed',
                      ""CanModify"" = false,
                      ""IsProcessedToOrder"" = true,
                      ""ConfirmedOrderId"" = @p0,
                      ""ConfirmedAt"" = @p1,
                      ""UpdatedAt"" = NOW() AT TIME ZONE 'UTC'
                  WHERE ""ScheduledOrderId"" = @p2",
                confirmedOrderId, confirmedAt, scheduledOrderId);
        }
    }
}
