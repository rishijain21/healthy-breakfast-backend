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

            // CHECK constraints
            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Subscriptions_Dates",
                    "\"EndDate\" > \"StartDate\"");
            });

            // Relationships
            builder.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(e => e.UserMeal)
                .WithMany()
                .HasForeignKey(e => e.UserMealId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(e => e.DeliveryAddress)
                .WithMany(a => a.Subscriptions)
                .HasForeignKey(e => e.DeliveryAddressId)
                .OnDelete(DeleteBehavior.SetNull);

            // ✅ FIX BUG 3: Partial unique index to prevent duplicate active subscriptions
            builder.HasIndex(e => new { e.UserId, e.UserMealId })
                .HasFilter("\"Active\" = true")
                .IsUnique()
                .HasDatabaseName("UX_Subscriptions_ActiveUserMeal");

            // Indexes
            builder.HasIndex(e => new { e.UserId, e.Active })
                .HasDatabaseName("IX_Subscriptions_UserId_Active");

            builder.HasIndex(e => new { e.Active, e.NextScheduledDate })
                .HasFilter("\"Active\" = true")
                .HasDatabaseName("IX_Subscriptions_Active_NextScheduledDate");
        }
    }
}