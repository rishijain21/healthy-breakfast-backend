using HealthyBreakfastApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HealthyBreakfastApp.Infrastructure.Data
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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Map OrderStatus property to "Status" column in Orders table
            modelBuilder.Entity<Order>(entity =>
            {
                entity.Property(e => e.OrderStatus)
                    .HasColumnName("Status")
                    .IsRequired();
            });

            // ========== SEED DATA WITH STATIC DATES ==========

            // Static date for all seed data
            var seedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            // Seed Ingredient Categories
            modelBuilder.Entity<IngredientCategory>().HasData(
                new IngredientCategory { CategoryId = 1, CategoryName = "Oats", CreatedAt = seedDate, UpdatedAt = seedDate },
                new IngredientCategory { CategoryId = 2, CategoryName = "Seeds", CreatedAt = seedDate, UpdatedAt = seedDate },
                new IngredientCategory { CategoryId = 3, CategoryName = "Fruits", CreatedAt = seedDate, UpdatedAt = seedDate },
                new IngredientCategory { CategoryId = 4, CategoryName = "Milk", CreatedAt = seedDate, UpdatedAt = seedDate },
                new IngredientCategory { CategoryId = 5, CategoryName = "Sweetener", CreatedAt = seedDate, UpdatedAt = seedDate }
            );

            // Seed Ingredients (WITH STATIC DATES)
            modelBuilder.Entity<Ingredient>().HasData(
                // Oats Category
                new Ingredient { IngredientId = 1, IngredientName = "Steel Cut Oats", Price = 25, Calories = 120, Protein = 4, Fiber = 4, CategoryId = 1, Available = true, Description = "High fiber, slow release energy", IconEmoji = "🥣", CreatedAt = seedDate, UpdatedAt = seedDate },
                new Ingredient { IngredientId = 2, IngredientName = "Rolled Oats", Price = 20, Calories = 150, Protein = 5, Fiber = 4, CategoryId = 1, Available = true, Description = "Classic choice, quick cooking", IconEmoji = "🌾", CreatedAt = seedDate, UpdatedAt = seedDate },
                new Ingredient { IngredientId = 3, IngredientName = "Quinoa Flakes", Price = 40, Calories = 110, Protein = 6, Fiber = 3, CategoryId = 1, Available = true, Description = "Complete protein, gluten-free", IconEmoji = "🌱", CreatedAt = seedDate, UpdatedAt = seedDate },

                // Seeds Category
                new Ingredient { IngredientId = 7, IngredientName = "Chia Seeds", Price = 15, Calories = 60, Protein = 3, Fiber = 5, CategoryId = 2, Available = true, Description = "Omega-3 rich superfood", IconEmoji = "🌰", CreatedAt = seedDate, UpdatedAt = seedDate },
                new Ingredient { IngredientId = 11, IngredientName = "Flax Seeds", Price = 12, Calories = 55, Protein = 2, Fiber = 3, CategoryId = 2, Available = true, Description = "Heart healthy fiber", IconEmoji = "🌾", CreatedAt = seedDate, UpdatedAt = seedDate },
                new Ingredient { IngredientId = 12, IngredientName = "Pumpkin Seeds", Price = 20, Calories = 85, Protein = 4, Fiber = 1, CategoryId = 2, Available = true, Description = "Magnesium powerhouse", IconEmoji = "🎃", CreatedAt = seedDate, UpdatedAt = seedDate },

                // Fruits Category
                new Ingredient { IngredientId = 20, IngredientName = "Fresh Blueberries", Price = 45, Calories = 42, Protein = 1, Fiber = 2, CategoryId = 3, Available = true, Description = "Antioxidant rich blueberries", IconEmoji = "🫐", CreatedAt = seedDate, UpdatedAt = seedDate },
                new Ingredient { IngredientId = 21, IngredientName = "Sliced Banana", Price = 15, Calories = 90, Protein = 1, Fiber = 3, CategoryId = 3, Available = true, Description = "Natural sweetness & potassium", IconEmoji = "🍌", CreatedAt = seedDate, UpdatedAt = seedDate },
                new Ingredient { IngredientId = 22, IngredientName = "Apple Chunks", Price = 25, Calories = 50, Protein = 0, Fiber = 2, CategoryId = 3, Available = true, Description = "Crispy texture & fiber", IconEmoji = "🍎", CreatedAt = seedDate, UpdatedAt = seedDate },

                // Milk Category
                new Ingredient { IngredientId = 30, IngredientName = "Almond Milk", Price = 20, Calories = 40, Protein = 1, Fiber = 0, CategoryId = 4, Available = true, Description = "Light and nutty plant milk", IconEmoji = "🥛", CreatedAt = seedDate, UpdatedAt = seedDate },
                new Ingredient { IngredientId = 32, IngredientName = "Greek Yogurt", Price = 35, Calories = 100, Protein = 15, Fiber = 0, CategoryId = 4, Available = true, Description = "High protein probiotic", IconEmoji = "🥄", CreatedAt = seedDate, UpdatedAt = seedDate },

                // Sweetener Category
                new Ingredient { IngredientId = 40, IngredientName = "Raw Honey", Price = 12, Calories = 65, Protein = 0, Fiber = 0, CategoryId = 5, Available = true, Description = "Natural unprocessed sweetener", IconEmoji = "🍯", CreatedAt = seedDate, UpdatedAt = seedDate },
                new Ingredient { IngredientId = 44, IngredientName = "No Sweetener", Price = 0, Calories = 0, Protein = 0, Fiber = 0, CategoryId = 5, Available = true, Description = "Natural fruit sweetness only", IconEmoji = "🚫", CreatedAt = seedDate, UpdatedAt = seedDate }
            );

            // Seed Meals
            modelBuilder.Entity<Meal>().HasData(
                new Meal { MealId = 1, MealName = "Classic Overnight Oats", Description = "Traditional overnight oats base", BasePrice = 40, CreatedAt = seedDate, UpdatedAt = seedDate },
                new Meal { MealId = 2, MealName = "Custom Breakfast Bowl", Description = "Build your perfect breakfast", BasePrice = 50, CreatedAt = seedDate, UpdatedAt = seedDate },
                new Meal { MealId = 3, MealName = "Protein Power Bowl", Description = "High protein breakfast option", BasePrice = 60, CreatedAt = seedDate, UpdatedAt = seedDate }
            );

            // Seed Test User
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    UserId = 1,
                    Name = "TestUser",
                    Email = "testuser@healthybreakfast.com",
                    Phone = "1234567890",
                    WalletBalance = 625,
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate
                }
            );

            // Seed Initial Wallet Balance for Test User
            modelBuilder.Entity<WalletTransaction>().HasData(
                new WalletTransaction
                {
                    TransactionId = 1,
                    UserId = 1,
                    Amount = 625,
                    Type = "Credit",
                    Description = "Initial wallet balance",
                    CreatedAt = seedDate // Using static date instead of DateTime.UtcNow
                }
            );
        }
    }
}
