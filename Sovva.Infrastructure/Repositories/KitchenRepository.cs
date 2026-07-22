using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sovva.Application.Interfaces;
using Sovva.Application.Helpers;
using Sovva.Domain.Entities;
using Sovva.Infrastructure.Data;

namespace Sovva.Infrastructure.Repositories
{
    internal class KitchenRepository : IKitchenRepository
    {
        private readonly AppDbContext _context;
        private readonly IAppTimeProvider _time;
        private readonly ILogger<KitchenRepository> _logger;

        public KitchenRepository(AppDbContext context, IAppTimeProvider time, ILogger<KitchenRepository> logger)
        {
            _context = context;
            _time = time;
            _logger = logger;
        }

        public async Task<List<Order>> GetOrdersForPreparationAsync(DateTime istDate, bool includeDetails = true)
        {
            // istDate is an IST calendar date (Kind=Unspecified, e.g. 2026-03-26 00:00:00)
            // Convert IST midnight → UTC to get the inclusive window PostgreSQL understands
            var windowStart = _time.ToUtc(istDate);              // 2026-03-25 18:30:00 UTC
            var windowEnd   = _time.ToUtc(istDate.AddDays(1));   // 2026-03-26 18:30:00 UTC

            _logger.LogInformation(
                "[KitchenRepo] IST date={IstDate:yyyy-MM-dd}  UTC window=[{Start:u}, {End:u})",
                istDate, windowStart, windowEnd);

            var query = _context.Orders.AsQueryable();

            if (includeDetails)
            {
                query = query
                    .Include(o => o.User)
                    .Include(o => o.UserMeal!)
                        .ThenInclude(um => um.UserMealIngredients)
                        .ThenInclude(umi => umi.Ingredient!)
                            .ThenInclude(i => i.IngredientCategory)
                    .Include(o => o.DeliveryAddress!)
                        .ThenInclude(da => da.ServiceableLocation);
            }
            else
            {
                // Basic loading for stats/aggregates
                query = query
                    .Include(o => o.UserMeal!)
                        .ThenInclude(um => um.UserMealIngredients)
                        .ThenInclude(umi => umi.Ingredient!);
            }

            var orders = await query
                .Where(o =>
                    o.ScheduledFor >= windowStart &&
                    o.ScheduledFor <  windowEnd   &&
                    !o.IsPrepared)
                .OrderBy(o => o.CreatedAt)
                .ToListAsync();

            _logger.LogInformation("[KitchenRepo] Found {Count} orders in UTC window", orders.Count);

            foreach (var order in orders)
            {
                _logger.LogInformation(
                    "   - Order #{OrderId}: {MealName}, ScheduledFor: {ScheduledFor:u}, IsPrepared: {IsPrepared}",
                    order.OrderId, order.UserMeal?.MealName ?? "Custom", order.ScheduledFor, order.IsPrepared);
            }

            return orders;
        }

        public async Task<Order?> GetOrderByIdAsync(int orderId)
        {
            // Removed unnecessary includes for User, UserMeal, DeliveryAddress
            // The only caller MarkOrderAsPreparedAsync only needs order status and date.
            return await _context.Orders
                .FirstOrDefaultAsync(o => o.OrderId == orderId);
        }

        public async Task UpdateOrderAsync(Order order)
        {
            _context.Orders.Update(order);
            // NOTE: Caller (UnitOfWork or service) is responsible for SaveChangesAsync
        }
    }
}
