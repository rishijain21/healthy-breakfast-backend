using System.Threading.Tasks;
using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Domain.Entities;
using HealthyBreakfastApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HealthyBreakfastApp.Infrastructure.Repositories
{
    public class MealOptionRepository : IMealOptionRepository
    {
        private readonly AppDbContext _context;

        public MealOptionRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(MealOption entity)
        {
            await _context.MealOptions.AddAsync(entity);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        public async Task<MealOption?> GetByIdAsync(int id)
        {
            return await _context.MealOptions.FirstOrDefaultAsync(mo => mo.MealOptionId == id);
        }

        public async Task<IEnumerable<MealOption>> GetByMealIdAsync(int mealId)
        {
            return await _context.MealOptions
                .Where(mo => mo.MealId == mealId)
                .ToListAsync();
        }
    }
}
