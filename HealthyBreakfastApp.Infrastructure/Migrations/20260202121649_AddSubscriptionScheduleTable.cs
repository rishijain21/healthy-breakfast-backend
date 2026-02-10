using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthyBreakfastApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionScheduleTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create table only if it doesn't exist (for databases that already have it)
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN 
                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'SubscriptionSchedules') THEN
                        CREATE TABLE ""SubscriptionSchedules"" (
                            ""ScheduleId"" SERIAL PRIMARY KEY,
                            ""SubscriptionId"" INTEGER NOT NULL,
                            ""DayOfWeek"" INTEGER NOT NULL,
                            ""Quantity"" INTEGER NOT NULL,
                            ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL,
                            ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL,
                            CONSTRAINT ""CK_SubscriptionSchedules_DayOfWeek"" CHECK (""DayOfWeek"" >= 0 AND ""DayOfWeek"" <= 6),
                            CONSTRAINT ""CK_SubscriptionSchedules_Quantity"" CHECK (""Quantity"" > 0),
                            CONSTRAINT ""FK_SubscriptionSchedules_Subscriptions_SubscriptionId"" 
                                FOREIGN KEY (""SubscriptionId"") REFERENCES ""Subscriptions"" (""SubscriptionId"") ON DELETE CASCADE
                        );
                        
                        COMMENT ON COLUMN ""SubscriptionSchedules"".""DayOfWeek"" IS 'Day of week: 0=Sunday, 1=Monday, 2=Tuesday, 3=Wednesday, 4=Thursday, 5=Friday, 6=Saturday';
                        COMMENT ON COLUMN ""SubscriptionSchedules"".""Quantity"" IS 'Number of items to deliver on this day';
                        
                        CREATE UNIQUE INDEX IF NOT EXISTS ""IX_SubscriptionSchedules_Subscription_DayOfWeek"" 
                            ON ""SubscriptionSchedules"" (""SubscriptionId"", ""DayOfWeek"");
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubscriptionSchedules");
        }
    }
}
