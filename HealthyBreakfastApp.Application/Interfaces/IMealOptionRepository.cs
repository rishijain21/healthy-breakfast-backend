using HealthyBreakfastApp.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IMealOptionRepository
    {
        Task<IEnumerable<MealOption>> GetByMealIdAsync(int mealId);
        Task AddAsync(MealOption mealOption);
        Task SaveChangesAsync();
        
        // NEW METHODS
        Task DeleteAsync(MealOption mealOption);
    }
}
