using Sovva.Domain.Entities;
using System.Threading.Tasks;

namespace Sovva.Application.Interfaces
{
    public interface IMealOptionIngredientRepository
    {
        Task AddAsync(MealOptionIngredient mealOptionIngredient);
        Task SaveChangesAsync();
        
        // NEW METHOD
        Task DeleteByMealOptionIdAsync(int mealOptionId);
    }
}
