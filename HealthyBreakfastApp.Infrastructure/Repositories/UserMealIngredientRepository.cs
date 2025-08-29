using System.Threading.Tasks;
using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Domain.Entities;
using HealthyBreakfastApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HealthyBreakfastApp.Infrastructure.Repositories
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
            return await _context.UserMealIngredients.FirstOrDefaultAsync(umi => umi.UserMealIngredientId == id);
        }
    }
}
