using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sovva.Domain.Entities;

namespace Sovva.Infrastructure.Data.Configurations
{
    public class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
    {
        public void Configure(EntityTypeBuilder<Subscription> builder)
        {
            builder.HasKey(e => e.SubscriptionId);

            builder.Property(e => e.Frequency).HasConversion<int>().IsRequired();
            builder.Property(e => e.StartDate).IsRequired();
            builder.Property(e => e.EndDate).IsRequired();
            builder.Property(e => e.AgreedPrice).HasColumnType("decimal(18,2)");
            builder.Property(e => e.PauseReason).HasMaxLength(100);

            // CHECK constraints
            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Subscriptions_Dates",
                    "\"EndDate\" > \"StartDate\"");
                t.HasCheckConstraint("CK_Subscription_MealType",
                    "(\"MealId\" IS NOT NULL AND \"UserMealId\" IS NULL) OR (\"MealId\" IS NULL AND \"UserMealId\" IS NOT NULL)");
                t.HasCheckConstraint("CK_Subscriptions_AgreedPrice",
                    "\"AgreedPrice\" > 0");
            });

            // Relationships
            builder.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(e => e.UserMeal)
                .WithMany()
                .HasForeignKey(e => e.UserMealId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(e => e.Meal)
                .WithMany()
                .HasForeignKey(e => e.MealId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(e => e.DeliveryAddress)
                .WithMany(a => a.Subscriptions)
                .HasForeignKey(e => e.DeliveryAddressId)
                .OnDelete(DeleteBehavior.SetNull);

            // ✅ FIX BUG 3: Partial unique index to prevent duplicate active subscriptions
            builder.HasIndex(e => new { e.UserId, e.UserMealId })
                .HasFilter("\"IsActive\" = true AND \"UserMealId\" IS NOT NULL")
                .IsUnique()
                .HasDatabaseName("UX_Subscriptions_ActiveUserMeal");

            builder.HasIndex(e => new { e.UserId, e.MealId })
                .HasFilter("\"IsActive\" = true AND \"MealId\" IS NOT NULL")
                .IsUnique()
                .HasDatabaseName("UX_Subscriptions_ActiveMeal");

            // Indexes
            builder.HasIndex(e => new { e.UserId, e.IsActive })
                .HasDatabaseName("IX_Subscriptions_UserId_Active");

            builder.HasIndex(e => new { e.IsActive, e.NextScheduledDate })
                .HasFilter("\"IsActive\" = true")
                .HasDatabaseName("IX_Subscriptions_Active_NextScheduledDate");
        }
    }
}