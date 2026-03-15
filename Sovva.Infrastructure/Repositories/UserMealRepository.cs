using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;
using Sovva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Sovva.Infrastructure.Repositories
{
    public class UserMealRepository : IUserMealRepository
    {
        private readonly AppDbContext _context;

        public UserMealRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(UserMeal entity)
        {
            await _context.UserMeals.AddAsync(entity);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        public async Task<UserMeal?> GetByIdAsync(int id)
        {
            return await _context.UserMeals
                .Include(um => um.Meal)
                .AsNoTracking()
                .FirstOrDefaultAsync(um => um.UserMealId == id);
        }

        public async Task<IEnumerable<UserMeal>> GetByUserIdAsync(int userId)
        {
            return await _context.UserMeals
                .Include(um => um.Meal)
                .AsNoTracking()
                .Where(um => um.UserId == userId)
                .ToListAsync();
        }

        // ✅ NEW: Get UserMeal by UserId and MealId (for auto-find-or-create logic)
        public async Task<UserMeal?> GetByUserIdAndMealIdAsync(int userId, int mealId)
        {
            return await _context.UserMeals
                .Include(um => um.Meal)
                .AsNoTracking()
                .FirstOrDefaultAsync(um => um.UserId == userId && um.MealId == mealId);
        }
    }
}
