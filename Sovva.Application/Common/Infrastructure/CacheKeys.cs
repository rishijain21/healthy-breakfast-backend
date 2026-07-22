namespace Sovva.Application.Common.Infrastructure
{
    /// <summary>
    /// Centralized registry of cache keys used across the application.
    /// Follows the consistent naming convention: context:entity:identifier
    /// </summary>
    public static class CacheKeys
    {
        // ── Meals ──────────────────────────────────────────────────────────
        public static string MealsActive(int page, int pageSize) => $"meals:active:p{page}:s{pageSize}";
        public static string MealById(int id) => $"meals:id:{id}";
        public static string MealCategories() => "meals:categories_with_ingredients";

        // ── Ingredient Categories ──────────────────────────────────────────
        public static string CategoriesAll() => "categories:all";
        public static string CategoryById(int id) => $"categories:id:{id}";

        // ── Ingredients ────────────────────────────────────────────────────
        public static string IngredientsAll() => "ingredients:all";
        public static string IngredientsByCategory(int categoryId) => $"ingredients:category:{categoryId}";
        public static string IngredientById(int id) => $"ingredients:id:{id}";

        // ── Serviceable Locations ──────────────────────────────────────────
        public static string LocationsActive() => "locations:all:active";
        public static string LocationById(int id) => $"locations:id:{id}";

        // ── Users ──────────────────────────────────────────────────────────
        public static string UserById(int id) => $"user:id:{id}";
        public static string UserByAuthId(string authId) => $"user:auth:{authId}";

        // ── Subscriptions ──────────────────────────────────────────────────
        public static string SubscriptionsByUser(int userId) => $"subscriptions:user:{userId}";

        // ── Wallet ─────────────────────────────────────────────────────────
        public static string WalletBalance(int userId) => $"wallet:balance:{userId}";

        // ── Dashboard ──────────────────────────────────────────────────────
        public static string DashboardProfile(int userId) => $"dashboard:profile:{userId}";
        public static string ActiveSubscriptions(int userId) => $"dashboard:active_subscriptions:{userId}";
    }
}
