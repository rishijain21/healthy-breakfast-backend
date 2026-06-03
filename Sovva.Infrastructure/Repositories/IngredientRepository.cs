using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;
using Sovva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Sovva.Infrastructure.Repositories
{
    internal class IngredientRepository : IIngredientRepository
    {
        private readonly AppDbContext _context;
        private readonly ICacheService _cacheService;
        private const string CacheKeyAll = "Ingredients_All";

        public IngredientRepository(AppDbContext context, ICacheService cacheService)
        {
            _context = context;
            _cacheService = cacheService;
        }

        // ==================== READ OPERATIONS ====================
        
        public async Task<IEnumerable<Ingredient>> GetAllAsync()
        {
            var cached = await _cacheService.GetAsync<IEnumerable<Ingredient>>(CacheKeyAll);
            if (cached != null)
                return cached;

            var data = await _context.Ingredients.AsNoTracking().ToListAsync();
            await _cacheService.SetAsync(CacheKeyAll, data, TimeSpan.FromHours(12));
            return data;
        }

        public async Task<IEnumerable<Ingredient>> GetByCategoryIdAsync(int categoryId)
        {
            return await _context.Ingredients
                .AsNoTracking()
                .Where(i => i.CategoryId == categoryId)
                .ToListAsync();
        }

        public async Task<Ingredient?> GetByIdAsync(int id)
        {
            return await _context.Ingredients
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.IngredientId == id);
        }

        public async Task<Ingredient?> GetByIdWithCategoryAsync(int id)
        {
            return await _context.Ingredients
                .AsNoTracking()
                .Include(i => i.IngredientCategory)
                .FirstOrDefaultAsync(i => i.IngredientId == id);
        }

        public async Task<Dictionary<int, Ingredient>> GetByIdsAsync(IEnumerable<int> ids)
        {
            return await _context.Ingredients
                .AsNoTracking()
                .Include(i => i.IngredientCategory)
                .Where(i => ids.Contains(i.IngredientId))
                .ToDictionaryAsync(i => i.IngredientId);
        }

        // ==================== CREATE OPERATIONS ====================
        
        public async Task AddIngredientAsync(Ingredient ingredient)
        {
            await _context.Ingredients.AddAsync(ingredient);
            await _cacheService.RemoveAsync(CacheKeyAll);
        }

        // ==================== UPDATE OPERATIONS ====================
        
        public Task UpdateIngredientAsync(Ingredient ingredient)
        {
            _context.Ingredients.Update(ingredient);
            _cacheService.RemoveAsync(CacheKeyAll).GetAwaiter().GetResult();
            return Task.CompletedTask;
        }

        // ==================== DELETE OPERATIONS ====================
        
        public Task DeleteIngredientAsync(Ingredient ingredient)
        {
            _context.Ingredients.Remove(ingredient);
            _cacheService.RemoveAsync(CacheKeyAll).GetAwaiter().GetResult();
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
