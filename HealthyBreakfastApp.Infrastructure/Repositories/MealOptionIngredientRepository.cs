using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Domain.Entities;
using HealthyBreakfastApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.Infrastructure.Repositories
{
    public class MealOptionIngredientRepository : IMealOptionIngredientRepository
    {
        private readonly AppDbContext _context;

        public MealOptionIngredientRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(MealOptionIngredient mealOptionIngredient)
        {
            await _context.MealOptionIngredients.AddAsync(mealOptionIngredient);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        // NEW METHOD
        public async Task DeleteByMealOptionIdAsync(int mealOptionId)
        {
            var ingredients = await _context.MealOptionIngredients
                .Where(moi => moi.MealOptionId == mealOptionId)
                .ToListAsync();
            
            _context.MealOptionIngredients.RemoveRange(ingredients);
            await _context.SaveChangesAsync();
        }
    }
}
