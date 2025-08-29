using System.Threading.Tasks;
using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Domain.Entities;
using HealthyBreakfastApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HealthyBreakfastApp.Infrastructure.Repositories
{
    public class MealOptionIngredientRepository : IMealOptionIngredientRepository
    {
        private readonly AppDbContext _context;

        public MealOptionIngredientRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(MealOptionIngredient entity)
        {
            await _context.MealOptionIngredients.AddAsync(entity);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        public async Task<MealOptionIngredient?> GetByIdAsync(int id)
        {
            return await _context.MealOptionIngredients.FirstOrDefaultAsync(moi => moi.MealOptionIngredientId == id);
        }
    }
}
