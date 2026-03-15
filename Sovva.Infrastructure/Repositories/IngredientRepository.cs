using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;
using Sovva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Sovva.Infrastructure.Repositories
{
    public class IngredientRepository : IIngredientRepository
    {
        private readonly AppDbContext _context;

        public IngredientRepository(AppDbContext context)
        {
            _context = context;
        }

        // ==================== READ OPERATIONS ====================
        
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

        public async Task<Ingredient?> GetByIdAsync(int id)
        {
            return await _context.Ingredients
                .FirstOrDefaultAsync(i => i.IngredientId == id);
        }

        public async Task<Ingredient?> GetByIdWithCategoryAsync(int id)
        {
            return await _context.Ingredients
                .Include(i => i.IngredientCategory)
                .FirstOrDefaultAsync(i => i.IngredientId == id);
        }

        // ==================== CREATE OPERATIONS ====================
        
        public async Task AddIngredientAsync(Ingredient ingredient)
        {
            await _context.Ingredients.AddAsync(ingredient);
        }

        // ==================== UPDATE OPERATIONS ====================
        
        public Task UpdateIngredientAsync(Ingredient ingredient)
        {
            _context.Ingredients.Update(ingredient);
            return Task.CompletedTask;
        }

        // ==================== DELETE OPERATIONS ====================
        
        public Task DeleteIngredientAsync(Ingredient ingredient)
        {
            _context.Ingredients.Remove(ingredient);
            return Task.CompletedTask;
        }

        // ==================== CHECK OPERATIONS ====================
        
        public async Task<bool> IsIngredientUsedInMealsAsync(int ingredientId)
        {
            // Check if ingredient is used in MealOptionIngredients
            return await _context.MealOptionIngredients
                .AnyAsync(moi => moi.IngredientId == ingredientId);
        }

        // ==================== SAVE ====================
        
        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
