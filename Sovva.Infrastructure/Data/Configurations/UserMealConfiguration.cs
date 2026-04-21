using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sovva.Domain.Entities;

namespace Sovva.Infrastructure.Data.Configurations
{
    public class UserMealConfiguration : IEntityTypeConfiguration<UserMeal>
    {
        public void Configure(EntityTypeBuilder<UserMeal> builder)
        {
            builder.HasKey(e => e.UserMealId);

            builder.Property(e => e.MealName).IsRequired().HasMaxLength(200);
            builder.Property(e => e.TotalPrice)
                .HasColumnType("decimal(12,2)")
                .IsRequired();

            // Relationships
            builder.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(e => e.Meal)
                .WithMany()
                .HasForeignKey(e => e.MealId)
                .OnDelete(DeleteBehavior.Restrict);

            // ✅ FIX BUG 4: Unique index to prevent duplicate UserMeal rows
            builder.HasIndex(e => new { e.UserId, e.MealId })
                .IsUnique()
                .HasDatabaseName("UX_UserMeals_UserId_MealId");
        }
    }
}