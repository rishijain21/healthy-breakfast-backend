using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Domain.Entities;
using HealthyBreakfastApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.Infrastructure.Repositories
{
    public class IngredientRepository : IIngredientRepository
    {
        private readonly AppDbContext _context;

        public IngredientRepository(AppDbContext context)
        {
            _context = context;
        }

        // REMOVE .Include() calls for now
        public async Task<IEnumerable<Ingredient>> GetAllAsync()
        {
            return await _context.Ingredients.ToListAsync();
        }

        public async Task<IEnumerable<Ingredient>> GetByCategoryIdAsync(int categoryId)
        {
            return await _context.Ingredients
                .Where(i => i.CategoryId == categoryId)
                .ToListAsync();
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
            return await _context.Ingredients
                .FirstOrDefaultAsync(i => i.IngredientId == id);
        }

        public async Task<Ingredient?> GetByIdWithCategoryAsync(int id)
        {
            return await _context.Ingredients
                .FirstOrDefaultAsync(i => i.IngredientId == id);
        }
    }
}
