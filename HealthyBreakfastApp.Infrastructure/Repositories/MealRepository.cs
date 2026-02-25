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

        // ✅ Public method for meal builder
        public async Task<IEnumerable<Meal>> GetActiveMealsAsync()
        {
            // Returns all meals - if you add IsActive field to Meal entity, filter here
            return await _context.Meals.ToListAsync();
        }

        // NEW METHODS
        public async Task<Meal?> GetByIdWithOptionsAsync(int id)
        {
            return await _context.Meals
                .Include(m => m.MealOptions)
                    .ThenInclude(mo => mo.IngredientCategory)
                .Include(m => m.MealOptions)
                    .ThenInclude(mo => mo.MealOptionIngredients)
                        .ThenInclude(moi => moi.Ingredient)
                .FirstOrDefaultAsync(m => m.MealId == id);
        }

        public async Task UpdateMealAsync(Meal meal)
        {
            _context.Meals.Update(meal);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteMealAsync(Meal meal)
        {
            _context.Meals.Remove(meal);
            await _context.SaveChangesAsync();
        }
    }
}
