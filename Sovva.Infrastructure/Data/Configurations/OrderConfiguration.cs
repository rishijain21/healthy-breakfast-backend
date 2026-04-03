using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sovva.Domain.Entities;

namespace Sovva.Infrastructure.Data.Configurations
{
    public class OrderConfiguration : IEntityTypeConfiguration<Order>
    {
        public void Configure(EntityTypeBuilder<Order> builder)
        {
            builder.HasKey(e => e.OrderId);

            builder.Property(e => e.TotalPrice).HasColumnType("decimal(12,2)");

            builder.Property(e => e.OrderStatus)
                .HasColumnName("Status")
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(50);

            // CHECK constraints
            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Orders_Status",
                    "\"Status\" IN ('Pending','Confirmed','Preparing','OutForDelivery','Delivered','Cancelled')");

                t.HasCheckConstraint("CK_Orders_TotalPrice",
                    "\"TotalPrice\" >= 0");
            });

            // Relationships
            builder.HasOne(e => e.DeliveryAddress)
                .WithMany(a => a.Orders)
                .HasForeignKey(e => e.DeliveryAddressId)
                .OnDelete(DeleteBehavior.SetNull);

            // Single FK to ScheduledOrders — removes the duplicate FK bug
            builder.HasOne(e => e.SourceScheduledOrder)
                .WithOne()
                .HasForeignKey<Order>(e => e.ScheduledOrderId)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes
            builder.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_Orders_UserId");

            builder.HasIndex(e => e.ScheduledFor)
                .HasDatabaseName("IX_Orders_ScheduledFor");

            builder.HasIndex(e => new { e.UserId, e.OrderStatus })
                .HasDatabaseName("IX_Orders_UserId_Status");
        }
    }
}