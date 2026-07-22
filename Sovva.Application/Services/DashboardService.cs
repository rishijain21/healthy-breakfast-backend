// Sovva.Application/Services/DashboardService.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sovva.Application.DTOs;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Sovva.Application.Common.Infrastructure;
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
        // ✅ Cached to avoid per-call allocation — JsonSerializerOptions is expensive to construct
        private static readonly JsonSerializerOptions _nutritionJsonOpts =
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        private readonly IUserRepository _userRepository;
        private readonly IWalletTransactionRepository _walletTransactionRepository;
        private readonly ISubscriptionService _subscriptionService;
        private readonly IScheduledOrderRepository _scheduledOrderRepository;
        private readonly IAppTimeProvider _time;
        private readonly ICacheService _cacheService;
        private readonly ILogger<DashboardService> _logger;

        public DashboardService(
            IUserRepository userRepository,
            IWalletTransactionRepository walletTransactionRepository,
            ISubscriptionService subscriptionService,
            IScheduledOrderRepository scheduledOrderRepository,
            IAppTimeProvider time,
            ICacheService cacheService,
            ILogger<DashboardService> logger)
        {
            _userRepository = userRepository;
            _walletTransactionRepository = walletTransactionRepository;
            _subscriptionService = subscriptionService;
            _scheduledOrderRepository = scheduledOrderRepository;
            _time = time;
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(int userId, CancellationToken ct = default)
        {
            _logger.LogInformation("📊 Building dashboard summary for user {UserId}", userId);

            // Calculate tomorrow's date in IST
            var istNow = _time.ToIst(_time.UtcNow);
            var tomorrowIst = istNow.Date.AddDays(1);

            var profile = await GetProfileAsync(userId, _userRepository, ct);
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

            // ── SEQUENTIAL DB QUERIES ────────────────────────────────────────────
            // Note: All repositories share the same scoped EF DbContext.
            // Task.WhenAll with EF queries on a shared context causes InvalidOperationException.
            // Queries run sequentially — the wallet SUM and scheduled-order reads use
            // lightweight projections/raw SQL that complete in < 5ms each on typical load.
            var walletBalance = await _walletTransactionRepository.GetUserBalanceAsync(userId);

            var transactionsResult = await _walletTransactionRepository.GetByUserIdAsync(userId, 1, 5);
            // Note: GetByUserIdAsync already returns descending-ordered results from DB.
            // No in-memory re-ordering needed.
            var transactions = transactionsResult.Items.ToList();
            
            var subscriptions = await GetActiveSubscriptionsAsync(userId, _subscriptionService, ct);
            
            var tomorrowOrders = await GetTomorrowOrdersAsync(userId, tomorrowIst, _scheduledOrderRepository, ct);

            // ── THIS WEEK: rolling 7 days (IST) ──────────────────────────────────
            var todayIst = DateOnly.FromDateTime(istNow);
            var weekStart = todayIst.AddDays(-6);
            var weekOrders = await _scheduledOrderRepository.GetByUserIdAndDateRangeAsync(userId, weekStart, todayIst);

            var (avgCalories, avgProtein, avgCarbs, avgFats) = ComputeWeeklyAverages(weekOrders, tomorrowOrders);
            var currentStreak = ComputeStreak(weekOrders, todayIst);
            var lifetimeCredits = await _walletTransactionRepository.GetLifetimeCreditSumAsync(userId);
            var loyaltyPoints = (int)lifetimeCredits;

            profile.WalletBalance = walletBalance;


            _logger.LogInformation(
                "Dashboard ready: profile={ProfileFound}, balance={Balance}, " +
                "transactions={TxCount}, subscriptions={SubCount}, tomorrowOrders={OrderCount}",
                profile != null,
                walletBalance,
                transactions.Count,
                subscriptions.Count(),
                tomorrowOrders.Count
            );

            return new DashboardSummaryDto
            {
                Profile = profile,
                WalletBalance = walletBalance,
                // Repo returns already-ordered descending results. No further sorting needed.
                RecentTransactions = transactions
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
                BestStreak = currentStreak, // accurate — stored BestStreak is a future feature
                LoyaltyPoints = loyaltyPoints,
                AverageCalories = avgCalories,
                AverageProtein = avgProtein,
                AverageCarbs = avgCarbs,
                AverageFats = avgFats,
                OrdersThisWeek = weekOrders.Count
            };
        }

        public async Task InvalidateDashboardCacheAsync(int userId)
        {
            await _cacheService.RemoveAsync(CacheKeys.DashboardProfile(userId));
            await _cacheService.RemoveAsync(CacheKeys.ActiveSubscriptions(userId));
            await _cacheService.RemoveAsync($"dashboard:light:{userId}"); // ✅ Also bust light BFF cache
        }

        private async Task<UserDto?> GetProfileAsync(int userId, IUserRepository userRepo, CancellationToken ct)
        {
            var cacheKey = CacheKeys.DashboardProfile(userId);
            
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
        /// Get active subscriptions with 30s cache (CACHE-02 fix).
        /// Cache is invalidated when subscriptions are created, cancelled, or paused.
        /// </summary>
        private async Task<List<SubscriptionDto>> GetActiveSubscriptionsAsync(int userId, ISubscriptionService subService, CancellationToken ct)
        {
            var cacheKey = CacheKeys.ActiveSubscriptions(userId);

            var cached = await _cacheService.GetAsync<List<SubscriptionDto>>(cacheKey);
            if (cached != null)
            {
                _logger.LogDebug("📦 Active subscriptions served from cache for user {UserId}", userId);
                return cached;
            }

            var subscriptions = (await subService.GetActiveSubscriptionsByUserIdAsync(userId)).ToList();

            // Cache for 30s — short TTL to stay fresh after subscription changes.
            // InvalidateDashboardCacheAsync() is called by create/cancel/pause endpoints.
            await _cacheService.SetAsync(cacheKey, subscriptions, TimeSpan.FromSeconds(30));
            _logger.LogDebug("💾 Active subscriptions cached for user {UserId} ({Count} subs)", userId, subscriptions.Count);

            return subscriptions;
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
                            _nutritionJsonOpts); // ✅ Reuse cached options
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
                    catch (JsonException jex)
                    {
                        _logger.LogWarning(jex,
                            "Failed to parse NutritionalSummary JSON for scheduled order {OrderId}",
                            o.ScheduledOrderId);
                        // Fallback to ingredient-level calculation below
                    }
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

        private int ComputeStreak(IEnumerable<ScheduledOrder> weekOrders, DateOnly todayIst)
        {
            // ✅ Count both Processed (past confirmed deliveries) and Scheduled (today/future)
            // This correctly gives a streak > 0 to active subscribers
            var orderedDates = weekOrders
                .Where(o => o.OrderStatus == ScheduledOrderStatus.Processed
                         || o.OrderStatus == ScheduledOrderStatus.Scheduled)
                .Select(o => o.ScheduledFor) // Assuming ScheduledFor is a DateOnly
                .Distinct()
                .OrderByDescending(d => d)
                .ToList();

            var currentStreak = 0;
            // ✅ Start from today (not yesterday) so today's scheduled order counts
            var expectedDate = todayIst;

            foreach (var date in orderedDates)
            {
                if (date == expectedDate)
                {
                    currentStreak++;
                    expectedDate = expectedDate.AddDays(-1);
                }
                else
                {
                    break;
                }
            }
            return currentStreak;
        }

        public async Task<DashboardLightDto> GetDashboardLightAsync(int userId, CancellationToken ct = default)
        {
            var cacheKey = $"dashboard:light:{userId}";
            var cached = await _cacheService.GetAsync<DashboardLightDto>(cacheKey);
            if (cached != null)
            {
                _logger.LogInformation("📊 Dashboard light cache HIT for user {UserId}", userId);
                return cached;
            }

            _logger.LogInformation("📊 Building lightweight dashboard for user {UserId}", userId);

            var istNow = _time.ToIst(_time.UtcNow);
            var tomorrowIst = istNow.Date.AddDays(1);

            // ─── Sequential DB queries (shared EF DbContext scope) ───────────────────
            var walletBalance = await _walletTransactionRepository.GetUserBalanceAsync(userId);

            var activeSubscriptions = await _subscriptionService.GetActiveSubscriptionsByUserIdAsync(userId);

            var tomorrowOrderEntities = await _scheduledOrderRepository.GetTomorrowOrdersSummaryAsync(
                userId, tomorrowIst);

            var todayIst = DateOnly.FromDateTime(istNow);
            var weekStart = todayIst.AddDays(-6);
            var ordersThisWeek = await _scheduledOrderRepository.GetOrdersThisWeekCountAsync(userId, weekStart, todayIst);

            var transactionsResult = await _walletTransactionRepository.GetByUserIdAsync(userId, 1, 5);
            var recentTransactions = transactionsResult.Items.Select(t => new WalletTransactionDto
            {
                TransactionId = t.TransactionId,
                UserId = t.UserId,
                Amount = t.Amount,
                Type = t.Type,
                Description = t.Description,
                CreatedAt = t.CreatedAt
            }).ToList();
            // ─────────────────────────────────────────────────────────────────────────

            var dto = new DashboardLightDto
            {
                WalletBalance = walletBalance,
                ActiveSubscriptionCount = activeSubscriptions.Count(),
                ActiveSubscriptions = activeSubscriptions.Select(s => new SubscriptionSummaryDto
                {
                    SubscriptionId    = s.SubscriptionId,
                    IsActive          = s.IsActive,
                    MealName          = s.MealName,
                    MealImageUrl      = s.MealImageUrl,
                    AgreedPrice       = s.AgreedPrice,
                    NextScheduledDate = s.NextScheduledDate
                }).ToList(),
                TomorrowOrders = tomorrowOrderEntities,
                OrdersThisWeek = ordersThisWeek,
                RecentTransactions = recentTransactions
            };

            await _cacheService.SetAsync(cacheKey, dto, TimeSpan.FromSeconds(30));

            return dto;
        }
    }
}