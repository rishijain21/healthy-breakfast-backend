using System.Threading.Tasks;
using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Domain.Entities;
using HealthyBreakfastApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HealthyBreakfastApp.Infrastructure.Repositories
{
    public class MealRepository : IMealRepository
    {
        private readonly AppDbContext _context;

        public MealRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddMealAsync(Meal meal)
        {
            await _context.Meals.AddAsync(meal);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        public async Task<Meal?> GetByIdAsync(int id)
        {
            return await _context.Meals.FirstOrDefaultAsync(m => m.MealId == id);
        }

        public async Task<IEnumerable<Meal>> GetAllAsync()
        {
            return await _context.Meals.ToListAsync();
        }
    }
}
