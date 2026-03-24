// Sovva.Application/Services/DashboardService.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Services
{
    /// <summary>
    /// Dashboard aggregation service - runs 5 parallel queries for fast login bootstrap
    /// </summary>
    public class DashboardService : IDashboardService
    {
        private readonly IUserRepository _userRepository;
        private readonly IWalletTransactionRepository _walletTransactionRepository;
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly IScheduledOrderRepository _scheduledOrderRepository;
        private readonly IMemoryCache _cache;
        private readonly ILogger<DashboardService> _logger;
        
        private const string ProfileCacheKey = "dashboard:profile";
        private static readonly TimeZoneInfo IstZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

        public DashboardService(
            IUserRepository userRepository,
            IWalletTransactionRepository walletTransactionRepository,
            ISubscriptionRepository subscriptionRepository,
            IScheduledOrderRepository scheduledOrderRepository,
            IMemoryCache cache,
            ILogger<DashboardService> logger)
        {
            _userRepository = userRepository;
            _walletTransactionRepository = walletTransactionRepository;
            _subscriptionRepository = subscriptionRepository;
            _scheduledOrderRepository = scheduledOrderRepository;
            _cache = cache;
            _logger = logger;
        }

        public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(int userId, CancellationToken ct = default)
        {
            _logger.LogInformation("📊 Building dashboard summary for user {UserId}", userId);

            // Calculate tomorrow's date in IST
            var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IstZone);
            var tomorrowIst = istNow.Date.AddDays(1);

            // ✅ FIX: Sequential awaits — EF Core DbContext is NOT thread-safe.
            // Task.WhenAll with shared DbContext causes "second operation started" error.
            var profile = await GetProfileAsync(userId, ct);
            if (profile == null)
            {
                throw new InvalidOperationException("User not found");
            }

            var walletBalance = await _walletTransactionRepository.GetUserBalanceAsync(userId);
            var transactions = await _walletTransactionRepository.GetByUserIdAsync(userId);
            var subscriptions = await GetActiveSubscriptionsAsync(userId, ct);
            var tomorrowOrders = await GetTomorrowOrdersAsync(userId, tomorrowIst, ct);

            _logger.LogInformation(
                "✅ Dashboard ready: profile={ProfileFound}, balance={Balance}, " +
                "transactions={TxCount}, subscriptions={SubCount}, tomorrowOrders={OrderCount}",
                profile != null,
                walletBalance,
                transactions.Count(),
                subscriptions.Count(),
                tomorrowOrders.Count
            );

            return new DashboardSummaryDto
            {
                Profile = profile,
                WalletBalance = walletBalance,
                RecentTransactions = transactions
                    .OrderByDescending(t => t.CreatedAt)
                    .Take(20)
                    .Select(t => new WalletTransactionDto
                    {
                        TransactionId = t.TransactionId,
                        UserId = t.UserId,
                        Amount = t.Amount,
                        Type = t.Type,
                        Description = t.Description,
                        CreatedAt = t.CreatedAt
                    })
                    .ToList(),
                ActiveSubscriptions = subscriptions,
                TomorrowOrders = tomorrowOrders
            };
        }

        /// <summary>
        /// Get user profile with 5-minute cache (profile rarely changes)
        /// </summary>
        private async Task<UserDto?> GetProfileAsync(int userId, CancellationToken ct)
        {
            var cacheKey = $"{ProfileCacheKey}:{userId}";
            
            if (_cache.TryGetValue(cacheKey, out UserDto? cachedProfile))
            {
                _logger.LogDebug("📦 Profile served from cache for user {UserId}", userId);
                return cachedProfile;
            }

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return null;

            var profile = new UserDto
            {
                UserId = user.UserId,
                Name = user.Name,
                Email = user.Email,
                Phone = user.Phone,
                DeliveryAddress = user.DeliveryAddress,
                AccountStatus = user.AccountStatus,
                WalletBalance = user.WalletBalance,
                Role = user.Role,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                ProfileComplete = !string.IsNullOrWhiteSpace(user.Name) &&
                                !string.IsNullOrWhiteSpace(user.Phone) &&
                                !string.IsNullOrWhiteSpace(user.DeliveryAddress)
            };

            // Cache for 5 minutes
            _cache.Set(cacheKey, profile, TimeSpan.FromMinutes(5));
            _logger.LogDebug("💾 Profile cached for user {UserId}", userId);

            return profile;
        }

        /// <summary>
        /// Get active subscriptions (Active = true and within date range)
        /// </summary>
        private async Task<List<SubscriptionDto>> GetActiveSubscriptionsAsync(int userId, CancellationToken ct)
        {
            var subscriptions = await _subscriptionRepository.GetByUserIdAsync(userId);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            
            return subscriptions
                .Where(s => s.Active && s.StartDate <= today && s.EndDate >= today)
                .Select(s => new SubscriptionDto
                {
                    SubscriptionId = s.SubscriptionId,
                    UserId = s.UserId,
                    UserMealId = s.UserMealId,
                    Frequency = s.Frequency,
                    StartDate = s.StartDate,
                    EndDate = s.EndDate,
                    Active = s.Active,
                    NextScheduledDate = s.NextScheduledDate,
                    CreatedAt = s.CreatedAt,
                    UpdatedAt = s.UpdatedAt,
                    UserName = s.User?.Name ?? "",
                    MealName = s.UserMeal?.MealName ?? "",
                    MealPrice = s.UserMeal?.TotalPrice ?? 0,
                    WeeklySchedule = s.WeeklySchedule?.Select(ws => new WeeklyScheduleDto
                    {
                        DayOfWeek = ws.DayOfWeek,
                        Quantity = ws.Quantity
                    }).ToList() ?? new List<WeeklyScheduleDto>()
                })
                .ToList();
        }

        /// <summary>
        /// Get tomorrow's scheduled orders (cart) - only "scheduled" status
        /// </summary>
        private async Task<List<ScheduledOrderResponseDto>> GetTomorrowOrdersAsync(
            int userId, 
            DateTime tomorrowIstDate,
            CancellationToken ct)
        {
            // Get all orders for tomorrow by userId
            var allOrders = await _scheduledOrderRepository.GetByUserIdAndDateAsync(
                userId, 
                tomorrowIstDate
            );
            
            return allOrders
                .Where(o => o.OrderStatus.ToLower() == "scheduled")
                .Select(o => new ScheduledOrderResponseDto
                {
                    ScheduledOrderId = o.ScheduledOrderId,
                    MealName = o.MealName,
                    MealId = o.MealId,
                    MealImageUrl = o.MealImageUrl,
                    ScheduledFor = o.ScheduledFor,
                    DeliveryTimeSlot = o.DeliveryTimeSlot,
                    TotalPrice = o.TotalPrice,
                    OrderStatus = o.OrderStatus,
                    CanModify = o.CanModify,
                    CreatedAt = o.CreatedAt,
                    ExpiresAt = o.ExpiresAt,
                    SubscriptionId = o.SubscriptionId,
                    Ingredients = o.Ingredients?.Select(i => new ScheduledOrderIngredientDetailDto
                    {
                        IngredientId = i.IngredientId,
                        IngredientName = i.Ingredient?.IngredientName ?? "Ingredient",
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice,
                        TotalPrice = i.TotalPrice,
                        Category = i.Ingredient?.IngredientCategory?.CategoryName ?? "",
                        ImageUrl = i.Ingredient?.IconEmoji ?? ""
                    }).ToList() ?? new List<ScheduledOrderIngredientDetailDto>()
                })
                .ToList();
        }
    }
}