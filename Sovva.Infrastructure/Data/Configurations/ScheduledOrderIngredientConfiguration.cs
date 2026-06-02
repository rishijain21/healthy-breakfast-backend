using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sovva.Domain.Entities;

namespace Sovva.Infrastructure.Data.Configurations
{
    public class ScheduledOrderIngredientConfiguration : IEntityTypeConfiguration<ScheduledOrderIngredient>
    {
        public void Configure(EntityTypeBuilder<ScheduledOrderIngredient> builder)
        {
            builder.Property(e => e.UnitPrice).HasColumnType("decimal(12,2)");
            builder.Property(e => e.TotalPrice).HasColumnType("decimal(12,2)");
        }
    }
}
