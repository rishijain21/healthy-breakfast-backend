// HealthyBreakfastApp.Infrastructure/Data/AppDbContext.cs

using HealthyBreakfastApp.Domain.Entities;
using HealthyBreakfastApp.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System;

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
        public DbSet<UserAuthMapping> UserAuthMappings { get; set; }
        
        // ✅ Scheduled order tables
        public DbSet<ScheduledOrder> ScheduledOrders { get; set; }
        public DbSet<ScheduledOrderIngredient> ScheduledOrderIngredients { get; set; }
        
        // ✅ NEW: Subscription schedule table
        public DbSet<SubscriptionSchedule> SubscriptionSchedules { get; set; }

        // ✅ IST TIMEZONE: Configure for Indian timezone handling
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Enable legacy timestamp behavior for IST support
                AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            }
            
            base.OnConfiguring(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // UserAuthMapping configuration
            modelBuilder.Entity<UserAuthMapping>(entity =>
            {
                entity.HasKey(e => e.MappingId);
                entity.Property(e => e.MappingId).IsRequired();
                entity.Property(e => e.AuthId).IsRequired();
                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.CreatedAt).IsRequired();

                entity.HasIndex(e => e.AuthId).IsUnique();

                entity.HasOne(e => e.User)
                      .WithOne(u => u.AuthMapping)
                      .HasForeignKey<UserAuthMapping>(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.ToTable("user_auth_mapping");
                entity.Property(e => e.MappingId).HasColumnName("mapping_id");
                entity.Property(e => e.AuthId).HasColumnName("auth_id");
                entity.Property(e => e.UserId).HasColumnName("user_id");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            });

            // Map OrderStatus property to "Status" column in Orders table
            modelBuilder.Entity<Order>(entity =>
            {
                entity.Property(e => e.OrderStatus)
                    .HasColumnName("Status")
                    .IsRequired();
            });

            // ✅ ScheduledOrder configuration
            modelBuilder.Entity<ScheduledOrder>(entity =>
            {
                entity.HasKey(e => e.ScheduledOrderId);
                entity.Property(e => e.MealName).HasMaxLength(255);
                entity.Property(e => e.DeliveryTimeSlot).HasMaxLength(50);
                entity.Property(e => e.OrderStatus).HasMaxLength(50);
                entity.Property(e => e.TotalPrice).HasColumnType("decimal(10,2)");
                entity.Property(e => e.ScheduledFor).HasColumnType("date");
                
                // NEW FIELDS
                entity.Property(e => e.IsProcessedToOrder).HasDefaultValue(false);
                entity.Property(e => e.ConfirmedOrderId).IsRequired(false);
                
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ✅ ScheduledOrderIngredient configuration
            modelBuilder.Entity<ScheduledOrderIngredient>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UnitPrice).HasColumnType("decimal(10,2)");
                entity.Property(e => e.TotalPrice).HasColumnType("decimal(10,2)");
                
                entity.HasOne(e => e.ScheduledOrder)
                    .WithMany(so => so.Ingredients)
                    .HasForeignKey(e => e.ScheduledOrderId)
                    .OnDelete(DeleteBehavior.Cascade);
                    
                entity.HasOne(e => e.Ingredient)
                    .WithMany()
                    .HasForeignKey(e => e.IngredientId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ✅ Subscription configuration with enum conversion
            modelBuilder.Entity<Subscription>(entity =>
            {
                entity.HasKey(e => e.SubscriptionId);
                
                // Convert SubscriptionFrequency enum to integer for database storage
                entity.Property(e => e.Frequency)
                    .HasConversion<int>()
                    .IsRequired();
                
                entity.Property(e => e.StartDate).IsRequired();
                entity.Property(e => e.EndDate).IsRequired();
                entity.Property(e => e.Active).IsRequired();
                entity.Property(e => e.NextScheduledDate).IsRequired(false);
                
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(e => e.UserMeal)
                    .WithMany()
                    .HasForeignKey(e => e.UserMealId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ✅ NEW: SubscriptionSchedule configuration (for weekly subscriptions)
            modelBuilder.Entity<SubscriptionSchedule>(entity =>
            {
                entity.HasKey(e => e.ScheduleId);
                
                entity.Property(e => e.DayOfWeek)
                    .IsRequired()
                    .HasComment("Day of week: 0=Sunday, 1=Monday, 2=Tuesday, 3=Wednesday, 4=Thursday, 5=Friday, 6=Saturday");
                
                entity.Property(e => e.Quantity)
                    .IsRequired()
                    .HasComment("Number of items to deliver on this day");
                
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.Property(e => e.UpdatedAt).IsRequired();
                
                // Foreign key relationship
                entity.HasOne(e => e.Subscription)
                    .WithMany(s => s.WeeklySchedule)
                    .HasForeignKey(e => e.SubscriptionId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                // Prevent duplicate days for same subscription
                entity.HasIndex(e => new { e.SubscriptionId, e.DayOfWeek })
                    .IsUnique()
                    .HasDatabaseName("IX_SubscriptionSchedules_Subscription_DayOfWeek");
                
                // Check constraints
                entity.ToTable(t => t.HasCheckConstraint(
                    "CK_SubscriptionSchedules_DayOfWeek", 
                    "\"DayOfWeek\" >= 0 AND \"DayOfWeek\" <= 6"));
                
                entity.ToTable(t => t.HasCheckConstraint(
                    "CK_SubscriptionSchedules_Quantity", 
                    "\"Quantity\" > 0"));
            });

            // ========== SEED DATA ==========
            var seedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            // Seed Ingredient Categories
            modelBuilder.Entity<IngredientCategory>().HasData(
                new IngredientCategory { CategoryId = 1, CategoryName = "Oats", CreatedAt = seedDate, UpdatedAt = seedDate },
                new IngredientCategory { CategoryId = 2, CategoryName = "Seeds", CreatedAt = seedDate, UpdatedAt = seedDate },
                new IngredientCategory { CategoryId = 3, CategoryName = "Fruits", CreatedAt = seedDate, UpdatedAt = seedDate },
                new IngredientCategory { CategoryId = 4, CategoryName = "Milk", CreatedAt = seedDate, UpdatedAt = seedDate },
                new IngredientCategory { CategoryId = 5, CategoryName = "Sweetener", CreatedAt = seedDate, UpdatedAt = seedDate }
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

            // Seed Initial Wallet Balance
            modelBuilder.Entity<WalletTransaction>().HasData(
                new WalletTransaction
                {
                    TransactionId = 1,
                    UserId = 1,
                    Amount = 625,
                    Type = "Credit",
                    Description = "Initial wallet balance",
                    CreatedAt = seedDate
                }
            );
        }
    }
}
