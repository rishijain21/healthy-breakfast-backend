using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Domain.Entities;
using HealthyBreakfastApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.Infrastructure.Repositories
{
    public class IngredientCategoryRepository : IIngredientCategoryRepository
    {
        private readonly AppDbContext _context;

        public IngredientCategoryRepository(AppDbContext context)
        {
            _context = context;
        }

        // ADD THIS NEW METHOD ⬇️
        public async Task<IEnumerable<IngredientCategory>> GetAllAsync()
        {
            return await _context.IngredientCategories.ToListAsync();
        }

        public async Task AddAsync(IngredientCategory entity)
        {
            await _context.IngredientCategories.AddAsync(entity);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        public async Task<IngredientCategory?> GetByIdAsync(int id)
        {
            return await _context.IngredientCategories.FirstOrDefaultAsync(c => c.CategoryId == id);
        }
    }
}
