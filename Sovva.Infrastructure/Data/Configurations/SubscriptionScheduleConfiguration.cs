using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sovva.Domain.Entities;

namespace Sovva.Infrastructure.Data.Configurations
{
    public class SubscriptionScheduleConfiguration : IEntityTypeConfiguration<SubscriptionSchedule>
    {
        public void Configure(EntityTypeBuilder<SubscriptionSchedule> builder)
        {
            builder.ToTable(t => t.HasCheckConstraint(
                "CK_SubscriptionSchedule_DayOfWeek",
                "\"DayOfWeek\" >= 0 AND \"DayOfWeek\" <= 6"));
        }
    }
}
