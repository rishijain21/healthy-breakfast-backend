using System.Threading.Tasks;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;
using Sovva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Sovva.Infrastructure.Repositories
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
            return await _context.Meals.AsNoTracking()
                .FirstOrDefaultAsync(m => m.MealId == id && !m.IsDeleted);
        }

        public async Task<IEnumerable<Meal>> GetAllAsync()
        {
            // ✅ Filter out deleted meals
            return await _context.Meals.AsNoTracking()
                .Where(m => !m.IsDeleted)
                .ToListAsync();
        }

        // ✅ Public method for meal builder
        public async Task<IEnumerable<Meal>> GetActiveMealsAsync()
        {
            // ✅ Filter out deleted and incomplete meals
            return await _context.Meals.AsNoTracking()
                .Where(m => !m.IsDeleted)
                .ToListAsync();
        }

        // NEW METHODS
        public async Task<Meal?> GetByIdWithOptionsAsync(int id)
        {
            return await _context.Meals
                .AsNoTracking()
                .Include(m => m.MealOptions)
                    .ThenInclude(mo => mo.IngredientCategory)
                .Include(m => m.MealOptions)
                    .ThenInclude(mo => mo.MealOptionIngredients)
                        .ThenInclude(moi => moi.Ingredient)
                .FirstOrDefaultAsync(m => m.MealId == id && !m.IsDeleted);
        }

        public async Task UpdateMealAsync(Meal meal)
        {
            _context.Meals.Update(meal);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteMealAsync(Meal meal)
        {
            // ✅ Soft delete: mark as deleted instead of removing from DB
            meal.IsDeleted = true;
            _context.Meals.Update(meal);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> UpdateMealStatusAsync(int id, bool isComplete)
        {
            var meal = await _context.Meals.FindAsync(id);
            if (meal == null) return false;

            meal.IsComplete = isComplete;
            await _context.SaveChangesAsync();
            return true;
        }

        // ✅ NEW: Paginated admin list
        public async Task<(IEnumerable<Meal> Items, int TotalCount)> GetPagedAsync(int page, int pageSize)
        {
            var query = _context.Meals
                .AsNoTracking()
                .Where(m => !m.IsDeleted)
                .OrderBy(m => m.MealId);

            var totalCount = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        // ✅ NEW: Get all meals with options loaded (fixes N+1)
        public async Task<IEnumerable<Meal>> GetAllWithOptionsCountAsync()
        {
            return await _context.Meals
                .AsNoTracking()
                .Where(m => !m.IsDeleted)
                .Include(m => m.MealOptions)
                .OrderBy(m => m.MealId)
                .ToListAsync();
        }
    }
}
