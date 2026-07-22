using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;
using Sovva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace Sovva.Infrastructure.Repositories
{
    internal class MealOptionIngredientRepository : IMealOptionIngredientRepository
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

        // NEW METHOD
        public async Task DeleteByMealOptionIdAsync(int mealOptionId)
        {
            var ingredients = await _context.MealOptionIngredients
                .Where(moi => moi.MealOptionId == mealOptionId)
                .ToListAsync();
            
            _context.MealOptionIngredients.RemoveRange(ingredients);
            // NOTE: Caller (UnitOfWork or service) is responsible for SaveChangesAsync
        }
    }
}
