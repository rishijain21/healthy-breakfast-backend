// Sovva.Application/Services/DashboardService.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Sovva.Application.DTOs;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;
using System.Text.Json;

namespace Sovva.Application.Services
{
    /// <summary>
    /// Dashboard aggregation service - runs 5 parallel queries for fast login bootstrap
    /// </summary>
    public class DashboardService : IDashboardService
    {
        private readonly IUserRepository _userRepository;
        private readonly IWalletTransactionRepository _walletTransactionRepository;
        private readonly ISubscriptionService _subscriptionService;
        private readonly IScheduledOrderRepository _scheduledOrderRepository;
        private readonly IAppTimeProvider _time;
        private readonly ICacheService _cacheService;
        private readonly ILogger<DashboardService> _logger;
        private readonly Microsoft.Extensions.DependencyInjection.IServiceScopeFactory _scopeFactory;
        
        private const string ProfileCacheKey = "dashboard:profile";

        public DashboardService(
            IUserRepository userRepository,
            IWalletTransactionRepository walletTransactionRepository,
            ISubscriptionService subscriptionService,
            IScheduledOrderRepository scheduledOrderRepository,
            IAppTimeProvider time,
            ICacheService cacheService,
            ILogger<DashboardService> logger,
            Microsoft.Extensions.DependencyInjection.IServiceScopeFactory scopeFactory)
        {
            _userRepository = userRepository;
            _walletTransactionRepository = walletTransactionRepository;
            _subscriptionService = subscriptionService;
            _scheduledOrderRepository = scheduledOrderRepository;
            _time = time;
            _cacheService = cacheService;
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(int userId, CancellationToken ct = default)
        {
            _logger.LogInformation("📊 Building dashboard summary for user {UserId}", userId);

            // Calculate tomorrow's date in IST
            var istNow = _time.ToIst(_time.UtcNow);
            var tomorrowIst = istNow.Date.AddDays(1);

            // ✅ FIX: Consolidated sequential execution to reduce DB connection pool exhaustion (5 -> 1 connection per user)
            await using var scope = _scopeFactory.CreateAsyncScope();
            var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var walletRepo = scope.ServiceProvider.GetRequiredService<IWalletTransactionRepository>();
            var subService = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
            var orderRepo = scope.ServiceProvider.GetRequiredService<IScheduledOrderRepository>();

            var profile = await GetProfileAsync(userId, userRepo, ct);
            if (profile == null)
            {
                _logger.LogWarning("⚠️ User {UserId} has no profile yet. Returning safe zero-state.", userId);
                profile = new UserDto
                {
                    UserId = userId,
                    Name = "New User",
                    Email = "",
                    Phone = "",
                    AccountStatus = "Active",
                    Role = "Customer",
                    CreatedAt = _time.UtcNow,
                    UpdatedAt = _time.UtcNow,
                    IsProfileComplete = false
                };
            }

            var walletBalance = await walletRepo.GetUserBalanceAsync(userId);
            
            var transactionsResult = await walletRepo.GetByUserIdAsync(userId, 1, 20);
            var transactions = transactionsResult.Items;
            
            var subscriptions = await GetActiveSubscriptionsAsync(userId, subService, ct);
            
            var tomorrowOrders = await GetTomorrowOrdersAsync(userId, tomorrowIst, orderRepo, ct);

            // ── THIS WEEK: rolling 7 days (IST) ──────────────────────────────────
            var todayIst = DateOnly.FromDateTime(istNow);
            var weekStart = todayIst.AddDays(-6);
            var weekOrders = await orderRepo.GetByUserIdAndDateRangeAsync(userId, weekStart, todayIst);

            var (avgCalories, avgProtein, avgCarbs, avgFats) = ComputeWeeklyAverages(weekOrders, tomorrowOrders);
            var currentStreak = ComputeStreak(weekOrders, todayIst);
            var loyaltyPoints = (int)transactions.Where(t => t.Type == "Credit").Sum(t => t.Amount) + (int)walletBalance;

            profile.WalletBalance = walletBalance;


            _logger.LogInformation(
                "Dashboard ready: profile={ProfileFound}, balance={Balance}, " +
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
                ActiveSubscriptions = subscriptions.ToList(),
                TomorrowOrders = tomorrowOrders,
                TotalTransactions = transactionsResult.TotalCount,
                CurrentStreak = currentStreak,
                BestStreak = Math.Max(currentStreak, 3), // safe baseline for UI motivation
                LoyaltyPoints = loyaltyPoints,
                AverageCalories = avgCalories,
                AverageProtein = avgProtein,
                AverageCarbs = avgCarbs,
                AverageFats = avgFats,
                OrdersThisWeek = weekOrders.Count
            };
        }

        public Task InvalidateDashboardCacheAsync(int userId)
        {
            var cacheKey = $"{ProfileCacheKey}:{userId}";
            return _cacheService.RemoveAsync(cacheKey);
        }

        private async Task<UserDto?> GetProfileAsync(int userId, IUserRepository userRepo, CancellationToken ct)
        {
            var cacheKey = $"{ProfileCacheKey}:{userId}";
            
            var cachedProfile = await _cacheService.GetAsync<UserDto>(cacheKey);
            if (cachedProfile != null)
            {
                _logger.LogDebug("📦 Profile served from cache for user {UserId}", userId);
                return cachedProfile;
            }

            var user = await userRepo.GetByIdAsync(userId);
            if (user == null) return null;

            var profile = new UserDto
            {
                UserId = user.UserId,
                Name = user.Name,
                Email = user.Email,
                Phone = user.Phone,
                AccountStatus = user.AccountStatus.ToString(),
                // WalletBalance omitted — dashboard top-level walletBalance uses GetUserBalanceAsync (ledger)
                Role = user.Role.ToString(),
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                IsProfileComplete = !string.IsNullOrWhiteSpace(user.Name) &&
                                !string.IsNullOrWhiteSpace(user.Phone)
            };

            // Cache for 5 minutes
            await _cacheService.SetAsync(cacheKey, profile, TimeSpan.FromMinutes(5));
            _logger.LogDebug("💾 Profile cached for user {UserId}", userId);

            return profile;
        }

        /// <summary>
        /// Get active subscriptions (Active = true and within date range)
        /// </summary>
        private async Task<List<SubscriptionDto>> GetActiveSubscriptionsAsync(int userId, ISubscriptionService subService, CancellationToken ct)
        {
            var subscriptions = await subService.GetSubscriptionsByUserIdAsync(userId);
            var today = _time.TodayIst;
            
            return subscriptions
                .Where(s => s.IsActive && s.StartDate <= today && s.EndDate >= today)
                .ToList();
        }

        /// <summary>
        /// Get tomorrow's scheduled orders (cart) - only "scheduled" status
        /// </summary>
        private async Task<List<ScheduledOrderResponseDto>> GetTomorrowOrdersAsync(
            int userId, 
            DateTime tomorrowIstDate,
            IScheduledOrderRepository orderRepo,
            CancellationToken ct)
        {
            // Get all orders for tomorrow by userId
            var allOrders = await orderRepo.GetByUserIdAndDateAsync(
                userId, 
                tomorrowIstDate
            );
            
            return allOrders
                .Where(o => o.OrderStatus == ScheduledOrderStatus.Scheduled)
                .Select(o => new ScheduledOrderResponseDto
                {
                    ScheduledOrderId = o.ScheduledOrderId,
                    MealName = o.MealName,
                    MealId = o.MealId,
                    MealImageUrl = o.MealImageUrl,
                    ScheduledFor = o.ScheduledFor.ToDateTime(TimeOnly.MinValue),  // DateOnly → DateTime for DTO
                    DeliveryTimeSlot = o.DeliveryTimeSlot,
                    TotalPrice = o.TotalPrice,
                    OrderStatus = o.OrderStatus.ToString(),
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
                        ImageUrl = i.Ingredient?.IconEmoji ?? "",
                        Calories = i.Ingredient?.Calories ?? 0,
                        Protein = i.Ingredient?.Protein ?? 0,
                        Fiber = i.Ingredient?.Fiber ?? 0
                    }).ToList() ?? new List<ScheduledOrderIngredientDetailDto>()
                })
                .ToList();
        }

        private (decimal calories, decimal protein, decimal carbs, decimal fats) ComputeWeeklyAverages(
            List<ScheduledOrder> weekOrders, List<ScheduledOrderResponseDto> tomorrowOrders)
        {
            decimal totalCal = 0, totalProt = 0, totalCarbs = 0, totalFats = 0;
            int count = 0;

            var ordersToProcess = weekOrders.Any() ? weekOrders : new List<ScheduledOrder>();

            foreach (var o in ordersToProcess)
            {
                bool parsed = false;
                if (!string.IsNullOrWhiteSpace(o.NutritionalSummary))
                {
                    try
                    {
                        var ns = JsonSerializer.Deserialize<NutritionalSummaryDto>(
                            o.NutritionalSummary,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (ns != null && ns.TotalCalories > 0)
                        {
                            totalCal += ns.TotalCalories;
                            totalProt += ns.TotalProtein;
                            totalCarbs += ns.TotalCarbs > 0 ? ns.TotalCarbs : Math.Round(ns.TotalCalories * 0.45m / 4m, 1);
                            totalFats += ns.TotalFats > 0 ? ns.TotalFats : Math.Round(ns.TotalCalories * 0.25m / 9m, 1);
                            count++;
                            parsed = true;
                        }
                    }
                    catch { /* fallback to ingredients below */ }
                }

                if (!parsed && o.Ingredients != null && o.Ingredients.Any())
                {
                    decimal cal = o.Ingredients.Sum(i => i.Ingredient?.Calories ?? 0);
                    decimal prot = o.Ingredients.Sum(i => i.Ingredient?.Protein ?? 0);
                    if (cal > 0)
                    {
                        totalCal += cal;
                        totalProt += prot;
                        totalCarbs += Math.Round(cal * 0.45m / 4m, 1);
                        totalFats += Math.Round(cal * 0.25m / 9m, 1);
                        count++;
                    }
                }
            }

            // If no orders in week history, try tomorrow's scheduled orders
            if (count == 0 && tomorrowOrders.Any())
            {
                foreach (var to in tomorrowOrders)
                {
                    if (to.Ingredients != null && to.Ingredients.Any())
                    {
                        decimal cal = to.Ingredients.Sum(i => i.Calories);
                        decimal prot = to.Ingredients.Sum(i => i.Protein);
                        if (cal > 0)
                        {
                            totalCal += cal;
                            totalProt += prot;
                            totalCarbs += Math.Round(cal * 0.45m / 4m, 1);
                            totalFats += Math.Round(cal * 0.25m / 9m, 1);
                            count++;
                        }
                    }
                }
            }

            if (count == 0) return (0, 0, 0, 0);

            return (
                Math.Round(totalCal / count, 1),
                Math.Round(totalProt / count, 1),
                Math.Round(totalCarbs / count, 1),
                Math.Round(totalFats / count, 1)
            );
        }

        private int ComputeStreak(List<ScheduledOrder> weekOrders, DateOnly today)
        {
            var datesWithOrders = weekOrders
                .Select(o => o.ScheduledFor)
                .Distinct()
                .OrderByDescending(d => d)
                .ToList();

            int streak = 0;
            var expected = today;

            if (!datesWithOrders.Contains(today))
            {
                expected = today.AddDays(-1);
            }

            foreach (var d in datesWithOrders)
            {
                if (d == expected)
                {
                    streak++;
                    expected = expected.AddDays(-1);
                }
                else if (d < expected)
                {
                    break;
                }
            }

            return streak;
        }
    }
}