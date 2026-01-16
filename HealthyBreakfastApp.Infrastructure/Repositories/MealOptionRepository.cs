using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Domain.Entities;
using HealthyBreakfastApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.Infrastructure.Repositories
{
    public class MealOptionRepository : IMealOptionRepository
    {
        private readonly AppDbContext _context;

        public MealOptionRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<MealOption>> GetByMealIdAsync(int mealId)
        {
            return await _context.MealOptions
                .Include(mo => mo.IngredientCategory)
                .Include(mo => mo.MealOptionIngredients)
                    .ThenInclude(moi => moi.Ingredient)
                .Where(mo => mo.MealId == mealId)
                .ToListAsync();
        }

        public async Task AddAsync(MealOption mealOption)
        {
            await _context.MealOptions.AddAsync(mealOption);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        // NEW METHOD
        public async Task DeleteAsync(MealOption mealOption)
        {
            _context.MealOptions.Remove(mealOption);
        }
    }
}
