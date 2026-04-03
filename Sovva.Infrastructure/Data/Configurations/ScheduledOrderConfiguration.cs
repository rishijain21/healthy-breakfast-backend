using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;

namespace Sovva.Infrastructure.Data.Configurations
{
    public class ScheduledOrderConfiguration : IEntityTypeConfiguration<ScheduledOrder>
    {
        public void Configure(EntityTypeBuilder<ScheduledOrder> builder)
        {
            builder.HasKey(e => e.ScheduledOrderId);

            builder.Property(e => e.MealName).IsRequired().HasMaxLength(255);
            builder.Property(e => e.DeliveryTimeSlot).IsRequired().HasMaxLength(50);
            builder.Property(e => e.TotalPrice).HasColumnType("decimal(12,2)");
            builder.Property(e => e.ScheduledFor).HasColumnType("date");
            builder.Property(e => e.IsProcessedToOrder).HasDefaultValue(false);

            builder.Property(e => e.OrderStatus)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(50);

            // Timestamps
            builder.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");
            builder.Property(e => e.UpdatedAt).HasColumnType("timestamp with time zone");
            builder.Property(e => e.ConfirmedAt).HasColumnType("timestamp with time zone");
            builder.Property(e => e.ExpiresAt).HasColumnType("timestamp with time zone");

            // CHECK constraints
            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_ScheduledOrders_Status",
                    $"\"OrderStatus\" IN ('{ScheduledOrderStatus.Scheduled}', '{ScheduledOrderStatus.Confirmed}', '{ScheduledOrderStatus.Cancelled}', '{ScheduledOrderStatus.Processed}')");

                t.HasCheckConstraint("CK_ScheduledOrders_TotalPrice",
                    "\"TotalPrice\" >= 0");
            });

            // Relationships
            builder.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(e => e.DeliveryAddress)
                .WithMany()
                .HasForeignKey(e => e.DeliveryAddressId)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes
            builder.HasIndex(e => new { e.UserId, e.ScheduledFor })
                .HasDatabaseName("IX_ScheduledOrders_UserId_ScheduledFor");

            builder.HasIndex(e => new { e.AuthId, e.ScheduledFor })
                .HasDatabaseName("IX_ScheduledOrders_AuthId_ScheduledFor");

            builder.HasIndex(e => new { e.ScheduledFor, e.OrderStatus })
                .HasDatabaseName("IX_ScheduledOrders_ScheduledFor_Status");

            // Unique: one scheduled order per subscription per day
            builder.HasIndex(e => new { e.SubscriptionId, e.ScheduledFor })
                .IsUnique()
                .HasFilter("\"SubscriptionId\" IS NOT NULL")
                .HasDatabaseName("IX_ScheduledOrders_SubscriptionId_ScheduledFor_Unique");
        }
    }
}