using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sovva.Domain.Entities;

namespace Sovva.Infrastructure.Data.Configurations
{
    public class WalletTransactionConfiguration : IEntityTypeConfiguration<WalletTransaction>
    {
        public void Configure(EntityTypeBuilder<WalletTransaction> builder)
        {
            builder.HasKey(e => e.TransactionId);

            builder.Property(e => e.Amount).HasColumnType("decimal(12,2)");
            builder.Property(e => e.Type).IsRequired().HasMaxLength(20);
            builder.Property(e => e.Description).IsRequired().HasMaxLength(500);
            builder.Property(e => e.ReferenceType).HasMaxLength(50);

            // CHECK constraints
            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_WalletTransactions_Type",
                    "\"Type\" IN ('Credit', 'Debit')");

                t.HasCheckConstraint("CK_WalletTransactions_Amount",
                    "\"Amount\" > 0");

                t.HasCheckConstraint("CK_WalletTransactions_ReferenceType",
                    "\"ReferenceType\" IS NULL OR \"ReferenceType\" IN ('Order', 'Subscription', 'TopUp', 'Refund', 'Manual')");
            });

            // Indexes
            builder.HasIndex(e => new { e.UserId, e.CreatedAt })
                .HasDatabaseName("IX_WalletTransactions_UserId_CreatedAt");
        }
    }
}