using System.Threading.Tasks;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;
using Sovva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Sovva.Infrastructure.Repositories
{
    internal class MealRepository : IMealRepository
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

        // NOTE: All queries below do NOT need !m.IsDeleted or m.DeletedAt == null
        // because the Global Query Filter on AppDbContext handles this automatically.

        public async Task<Meal?> GetByIdAsync(int id)
        {
            return await _context.Meals.AsNoTracking()
                .FirstOrDefaultAsync(m => m.MealId == id);
        }

        public async Task<IEnumerable<Meal>> GetAllAsync()
        {
            return await _context.Meals.AsNoTracking()
                .ToListAsync();
        }

        public async Task<(IEnumerable<Meal> Items, int TotalCount)> GetActiveMealsAsync(int page, int pageSize)
        {
            var query = _context.Meals.AsNoTracking();

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderBy(m => m.MealId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<Meal?> GetByIdWithOptionsAsync(int id)
        {
            return await _context.Meals
                .AsNoTracking()
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
            // Soft delete: the TimestampInterceptor converts this Remove() call
            // into: meal.DeletedAt = now; (EntityState.Modified)
            _context.Meals.Remove(meal);
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

        public async Task<(IEnumerable<Meal> Items, int TotalCount)> GetPagedAsync(int page, int pageSize)
        {
            var query = _context.Meals
                .AsNoTracking()
                .OrderBy(m => m.MealId);

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<IEnumerable<Meal>> GetAllWithOptionsCountAsync()
        {
            return await _context.Meals
                .AsNoTracking()
                .Include(m => m.MealOptions)
                .OrderBy(m => m.MealId)
                .ToListAsync();
        }

        public async Task<List<Meal>> GetByIdsForUsersAsync(List<int> ids)
        {
            if (ids == null || ids.Count == 0)
                return new List<Meal>();

            return await _context.Meals
                .AsNoTracking()
                .Include(m => m.MealOptions)
                    .ThenInclude(mo => mo.IngredientCategory)
                .Include(m => m.MealOptions)
                    .ThenInclude(mo => mo.MealOptionIngredients)
                        .ThenInclude(moi => moi.Ingredient)
                .Where(m => ids.Contains(m.MealId) && m.IsComplete)
                .AsSplitQuery()
                .ToListAsync();
        }

        public async Task<List<Meal>> GetByIdsWithOptionsAsync(IEnumerable<int> ids)
        {
            return await _context.Meals
                .AsNoTracking()
                .Include(m => m.MealOptions)
                    .ThenInclude(mo => mo.IngredientCategory)
                .Include(m => m.MealOptions)
                    .ThenInclude(mo => mo.MealOptionIngredients)
                        .ThenInclude(moi => moi.Ingredient)
                .Where(m => ids.Contains(m.MealId))
                .AsSplitQuery()
                .ToListAsync();
        }
    }
}
