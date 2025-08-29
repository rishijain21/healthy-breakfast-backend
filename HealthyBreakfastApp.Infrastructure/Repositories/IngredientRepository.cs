using System.Threading.Tasks;
using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Domain.Entities;
using HealthyBreakfastApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HealthyBreakfastApp.Infrastructure.Repositories
{
    public class IngredientRepository : IIngredientRepository
    {
        private readonly AppDbContext _context;

        public IngredientRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddIngredientAsync(Ingredient ingredient)
        {
            await _context.Ingredients.AddAsync(ingredient);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        public async Task<Ingredient?> GetByIdAsync(int id)
        {
            return await _context.Ingredients.FirstOrDefaultAsync(i => i.IngredientId == id);
        }

        // ADD THIS NEW METHOD
        public async Task<Ingredient?> GetByIdWithCategoryAsync(int id)
        {
            return await _context.Ingredients
                .Include(i => i.IngredientCategory)
                .FirstOrDefaultAsync(i => i.IngredientId == id);
        }
    }
}
