using HealthyBreakfastApp.Domain.Entities;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IMealOptionIngredientRepository
    {
        Task AddAsync(MealOptionIngredient mealOptionIngredient);
        Task SaveChangesAsync();
        
        // NEW METHOD
        Task DeleteByMealOptionIdAsync(int mealOptionId);
    }
}
