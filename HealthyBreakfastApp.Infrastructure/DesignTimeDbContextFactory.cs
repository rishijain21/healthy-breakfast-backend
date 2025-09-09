using HealthyBreakfastApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HealthyBreakfastApp.Infrastructure
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            
            // Replace with your actual PostgreSQL connection string
            optionsBuilder.UseNpgsql("Host=localhost;Database=healthy_breakfast_db;Username=postgres;Password=Rishi@1234");
            
            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
