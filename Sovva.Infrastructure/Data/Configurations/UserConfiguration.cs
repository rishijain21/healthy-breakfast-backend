using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;

namespace Sovva.Infrastructure.Data.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.HasKey(e => e.UserId);

            builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
            builder.Property(e => e.Email).IsRequired().HasMaxLength(300);
            builder.Property(e => e.Phone).IsRequired().HasMaxLength(20);
            builder.Property(e => e.AccountStatus)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(e => e.Role)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(50);

            // Unique indexes
            builder.HasIndex(e => e.Email)
                .IsUnique()
                .HasDatabaseName("IX_Users_Email_Lower");

            builder.HasIndex(e => e.Phone)
                .IsUnique()
                .HasDatabaseName("IX_Users_Phone");

            // CHECK constraints
            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Users_Role",
                    $"\"Role\" IN ('{UserRole.Customer}', '{UserRole.Admin}', '{UserRole.DeliveryPartner}')");

                t.HasCheckConstraint("CK_Users_AccountStatus",
                    "\"AccountStatus\" IN ('Active', 'Deactivated', 'Deleted')");


            });

            // Soft delete — filtered index so queries on active users are fast
            builder.HasIndex(e => e.DeletedAt)
                .HasFilter("\"DeletedAt\" IS NULL")
                .HasDatabaseName("IX_Users_Active");
        }
    }
}