// Sovva.Infrastructure/Data/AppDbContext.cs

using Sovva.Domain.Entities;
using Sovva.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System;

namespace Sovva.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Meal> Meals { get; set; }
        public DbSet<IngredientCategory> IngredientCategories { get; set; }
        public DbSet<Ingredient> Ingredients { get; set; }
        public DbSet<MealOption> MealOptions { get; set; }
        public DbSet<MealOptionIngredient> MealOptionIngredients { get; set; }
        public DbSet<UserMeal> UserMeals { get; set; }
        public DbSet<UserMealIngredient> UserMealIngredients { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<Subscription> Subscriptions { get; set; }
        public DbSet<WalletTransaction> WalletTransactions { get; set; }
        public DbSet<UserAuthMapping> UserAuthMappings { get; set; }
        
        // Scheduled order tables
        public DbSet<ScheduledOrder> ScheduledOrders { get; set; }
        public DbSet<ScheduledOrderIngredient> ScheduledOrderIngredients { get; set; }
        
        // Subscription schedule table
        public DbSet<SubscriptionSchedule> SubscriptionSchedules { get; set; }
        
        // ✅ Location feature tables
        public DbSet<ServiceableLocation> ServiceableLocations { get; set; }
        public DbSet<UserAddress> UserAddresses { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
        
            
            base.OnConfiguring(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // All entity config loaded from IEntityTypeConfiguration<T> classes
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

            // Global soft-delete filter — excludes IsDeleted meals from all queries automatically
            modelBuilder.Entity<Meal>().HasQueryFilter(m => !m.IsDeleted);

            // ======= SEED DATA =======
            var seedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            modelBuilder.Entity<IngredientCategory>().HasData(
                new IngredientCategory { CategoryId = 1, CategoryName = "Oats",      CreatedAt = seedDate, UpdatedAt = seedDate },
                new IngredientCategory { CategoryId = 2, CategoryName = "Seeds",     CreatedAt = seedDate, UpdatedAt = seedDate },
                new IngredientCategory { CategoryId = 3, CategoryName = "Fruits",    CreatedAt = seedDate, UpdatedAt = seedDate },
                new IngredientCategory { CategoryId = 4, CategoryName = "Milk",      CreatedAt = seedDate, UpdatedAt = seedDate },
                new IngredientCategory { CategoryId = 5, CategoryName = "Sweetener", CreatedAt = seedDate, UpdatedAt = seedDate }
            );

            modelBuilder.Entity<Meal>().HasData(
                new Meal { MealId = 1, MealName = "Classic Overnight Oats",  Description = "Traditional overnight oats base",   BasePrice = 40, IsComplete = true, IsDeleted = false, CreatedAt = seedDate, UpdatedAt = seedDate },
                new Meal { MealId = 2, MealName = "Custom Breakfast Bowl",   Description = "Build your perfect breakfast",      BasePrice = 50, IsComplete = true, IsDeleted = false, CreatedAt = seedDate, UpdatedAt = seedDate },
                new Meal { MealId = 3, MealName = "Protein Power Bowl",      Description = "High protein breakfast option",     BasePrice = 60, IsComplete = true, IsDeleted = false, CreatedAt = seedDate, UpdatedAt = seedDate }
            );

            modelBuilder.Entity<User>().HasData(
                new User
                {
                    UserId        = 1,
                    Name          = "TestUser",
                    Email         = "testuser@sovva.com",
                    Phone         = "1234567890",
                    WalletBalance = 625,
                    Role          = UserRole.Customer,
                    CreatedAt     = seedDate,
                    UpdatedAt     = seedDate
                }
            );

            modelBuilder.Entity<WalletTransaction>().HasData(
                new WalletTransaction
                {
                    TransactionId = 1,
                    UserId        = 1,
                    Amount        = 625,
                    Type          = "Credit",
                    Description   = "Initial wallet balance",
                    ReferenceType = "Manual",
                    CreatedAt     = seedDate
                }
            );
        }
    }
}
