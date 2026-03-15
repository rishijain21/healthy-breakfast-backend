// Sovva.Infrastructure/Repositories/UserMealIngredientRepository.cs

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;
using Sovva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Sovva.Infrastructure.Repositories
{
    public class UserMealIngredientRepository : IUserMealIngredientRepository
    {
        private readonly AppDbContext _context;

        public UserMealIngredientRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(UserMealIngredient entity)
        {
            await _context.UserMealIngredients.AddAsync(entity);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        public async Task<UserMealIngredient?> GetByIdAsync(int id)
        {
            return await _context.UserMealIngredients
                .FirstOrDefaultAsync(umi => umi.UserMealIngredientId == id);
        }

        // ✅ NEW: Get all ingredients for a specific UserMeal with Ingredient details
        public async Task<IEnumerable<UserMealIngredient>> GetByUserMealIdAsync(int userMealId)
        {
            return await _context.UserMealIngredients
                .Include(umi => umi.Ingredient)  // Include ingredient details
                .Where(umi => umi.UserMealId == userMealId)
                .ToListAsync();
        }
    }
}
