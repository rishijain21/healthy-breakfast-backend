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

            // ✅ Order configuration with delivery address
            modelBuilder.Entity<Order>(entity =>
            {
                entity.Property(e => e.OrderStatus)
                    .HasColumnName("Status")
                    .HasConversion<string>()
                    .IsRequired();
                
                // ✅ ADD: Delivery address relationship
                entity.HasOne(e => e.DeliveryAddress)
                    .WithMany(a => a.Orders)
                    .HasForeignKey(e => e.DeliveryAddressId)
                    .OnDelete(DeleteBehavior.SetNull);

                // ✅ NEW: Link to source scheduled order
                entity.HasOne(e => e.SourceScheduledOrder)
                    .WithOne()
                    .HasForeignKey<Order>(e => e.ScheduledOrderId)
                    .OnDelete(DeleteBehavior.SetNull);

                // ✅ FIX 5: Add missing indexes
                entity.HasIndex(e => e.UserId)
                    .HasDatabaseName("IX_Orders_UserId");

                entity.HasIndex(e => e.OrderStatus)
                    .HasDatabaseName("IX_Orders_Status");

                entity.HasIndex(e => new { e.UserId, e.OrderStatus })
                    .HasDatabaseName("IX_Orders_UserId_Status");

                entity.HasIndex(e => e.ScheduledFor)
                    .HasDatabaseName("IX_Orders_ScheduledFor");
            });

            // ScheduledOrder configuration
            modelBuilder.Entity<ScheduledOrder>(entity =>
            {
                entity.HasKey(e => e.ScheduledOrderId);
                entity.Property(e => e.MealName).HasMaxLength(255);
                entity.Property(e => e.DeliveryTimeSlot).HasMaxLength(50);
                entity.Property(e => e.OrderStatus).HasMaxLength(50);
                entity.Property(e => e.TotalPrice).HasColumnType("decimal(10,2)");
                entity.Property(e => e.ScheduledFor).HasColumnType("date");
                
                // ✅ All timestamp columns are now timestamptz
                entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");
                entity.Property(e => e.UpdatedAt).HasColumnType("timestamp with time zone");
                entity.Property(e => e.ConfirmedAt).HasColumnType("timestamp with time zone");
                entity.Property(e => e.ExpiresAt).HasColumnType("timestamp with time zone");
                
                entity.Property(e => e.IsProcessedToOrder).HasDefaultValue(false);
                entity.Property(e => e.ConfirmedOrderId).IsRequired(false);
                
                // ✅ PERFORMANCE: Add indexes for common query patterns
                entity.HasIndex(e => e.ScheduledFor)
                    .HasDatabaseName("IX_ScheduledOrders_ScheduledFor");
                
                entity.HasIndex(e => new { e.AuthId, e.ScheduledFor })
                    .HasDatabaseName("IX_ScheduledOrders_AuthId_ScheduledFor");
                
                entity.HasIndex(e => new { e.ScheduledFor, e.OrderStatus })
                    .HasDatabaseName("IX_ScheduledOrders_ScheduledFor_OrderStatus");
                
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // ✅ ADD: Delivery address relationship
                entity.HasOne(e => e.DeliveryAddress)
                    .WithMany()
                    .HasForeignKey(e => e.DeliveryAddressId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ScheduledOrderIngredient configuration
            modelBuilder.Entity<ScheduledOrderIngredient>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UnitPrice).HasColumnType("decimal(10,2)");
                entity.Property(e => e.TotalPrice).HasColumnType("decimal(10,2)");
                
                // ✅ PERFORMANCE: Add index for foreign key lookups
                entity.HasIndex(e => e.ScheduledOrderId)
                    .HasDatabaseName("IX_ScheduledOrderIngredients_ScheduledOrderId");
                
                entity.HasOne(e => e.ScheduledOrder)
                    .WithMany(so => so.Ingredients)
                    .HasForeignKey(e => e.ScheduledOrderId)
                    .OnDelete(DeleteBehavior.Cascade);
                    
                entity.HasOne(e => e.Ingredient)
                    .WithMany()
                    .HasForeignKey(e => e.IngredientId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ✅ Subscription configuration with delivery address
            modelBuilder.Entity<Subscription>(entity =>
            {
                entity.HasKey(e => e.SubscriptionId);
                
                entity.Property(e => e.Frequency)
                    .HasConversion<int>()
                    .IsRequired();
                
                entity.Property(e => e.StartDate).IsRequired();
                entity.Property(e => e.EndDate).IsRequired();
                entity.Property(e => e.Active).IsRequired();
                entity.Property(e => e.NextScheduledDate).IsRequired(false);
                
                // ✅ PERFORMANCE: Add indexes for common query patterns
                entity.HasIndex(e => e.UserId)
                    .HasDatabaseName("IX_Subscriptions_UserId");
                
                entity.HasIndex(e => new { e.UserId, e.UserMealId })
                    .HasDatabaseName("IX_Subscriptions_UserId_UserMealId");
                
                entity.HasIndex(e => new { e.Active, e.StartDate, e.EndDate })
                    .HasDatabaseName("IX_Subscriptions_Active_StartDate_EndDate");
                
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(e => e.UserMeal)
                    .WithMany()
                    .HasForeignKey(e => e.UserMealId)
                    .OnDelete(DeleteBehavior.Restrict);
                
                // ✅ ADD: Delivery address relationship
                entity.HasOne(e => e.DeliveryAddress)
                    .WithMany(a => a.Subscriptions)
                    .HasForeignKey(e => e.DeliveryAddressId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // SubscriptionSchedule configuration
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
                
                entity.HasOne(e => e.Subscription)
                    .WithMany(s => s.WeeklySchedule)
                    .HasForeignKey(e => e.SubscriptionId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasIndex(e => new { e.SubscriptionId, e.DayOfWeek })
                    .IsUnique()
                    .HasDatabaseName("IX_SubscriptionSchedules_Subscription_DayOfWeek");
                
                entity.ToTable(t => t.HasCheckConstraint(
                    "CK_SubscriptionSchedules_DayOfWeek", 
                    "\"DayOfWeek\" >= 0 AND \"DayOfWeek\" <= 6"));
                
                entity.ToTable(t => t.HasCheckConstraint(
                    "CK_SubscriptionSchedules_Quantity", 
                    "\"Quantity\" > 0"));
            });

            // ✅ ServiceableLocation configuration
            modelBuilder.Entity<ServiceableLocation>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                entity.HasIndex(e => e.Pincode);
                entity.HasIndex(e => new { e.City, e.Area });
                
                entity.Property(e => e.Latitude).HasPrecision(10, 7);
                entity.Property(e => e.Longitude).HasPrecision(10, 7);
            });

            // ✅ UserAddress configuration
            modelBuilder.Entity<UserAddress>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                entity.HasIndex(e => e.UserId);
                
                // ✅ Unique constraint: Only ONE primary address per user
                entity.HasIndex(e => new { e.UserId, e.IsPrimary })
                    .IsUnique()
                    .HasFilter($"\"IsPrimary\" = true AND \"IsActive\" = true")
                    .HasDatabaseName("IX_UserAddresses_Primary_Unique");
                
                entity.HasOne(e => e.User)
                    .WithMany(u => u.Addresses)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(e => e.ServiceableLocation)
                    .WithMany(s => s.UserAddresses)
                    .HasForeignKey(e => e.ServiceableLocationId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ========== SEED DATA ==========
            var seedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            modelBuilder.Entity<IngredientCategory>().HasData(
                new IngredientCategory { CategoryId = 1, CategoryName = "Oats", CreatedAt = seedDate, UpdatedAt = seedDate },
                new IngredientCategory { CategoryId = 2, CategoryName = "Seeds", CreatedAt = seedDate, UpdatedAt = seedDate },
                new IngredientCategory { CategoryId = 3, CategoryName = "Fruits", CreatedAt = seedDate, UpdatedAt = seedDate },
                new IngredientCategory { CategoryId = 4, CategoryName = "Milk", CreatedAt = seedDate, UpdatedAt = seedDate },
                new IngredientCategory { CategoryId = 5, CategoryName = "Sweetener", CreatedAt = seedDate, UpdatedAt = seedDate }
            );

            modelBuilder.Entity<Meal>().HasData(
                new Meal { MealId = 1, MealName = "Classic Overnight Oats", Description = "Traditional overnight oats base", BasePrice = 40, IsComplete = true, IsDeleted = false, CreatedAt = seedDate, UpdatedAt = seedDate },
                new Meal { MealId = 2, MealName = "Custom Breakfast Bowl", Description = "Build your perfect breakfast", BasePrice = 50, IsComplete = true, IsDeleted = false, CreatedAt = seedDate, UpdatedAt = seedDate },
                new Meal { MealId = 3, MealName = "Protein Power Bowl", Description = "High protein breakfast option", BasePrice = 60, IsComplete = true, IsDeleted = false, CreatedAt = seedDate, UpdatedAt = seedDate }
            );

            // ✅ User configuration with optimistic concurrency for WalletBalance
            modelBuilder.Entity<User>(entity =>
            {
                entity.Property(u => u.WalletBalance)
                    .IsConcurrencyToken();
            });

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

            // ✅ FIX 8: Add indexes for WalletTransaction
            modelBuilder.Entity<WalletTransaction>(entity =>
            {
                entity.HasIndex(e => e.UserId)
                    .HasDatabaseName("IX_WalletTransactions_UserId");
                entity.HasIndex(e => new { e.UserId, e.CreatedAt })
                    .HasDatabaseName("IX_WalletTransactions_UserId_CreatedAt");
            });

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
