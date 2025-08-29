using HealthyBreakfastApp.Domain.Entities;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IMealOptionIngredientRepository
    {
        Task AddAsync(MealOptionIngredient entity);
        Task SaveChangesAsync();
        Task<MealOptionIngredient?> GetByIdAsync(int id);
    }
}
