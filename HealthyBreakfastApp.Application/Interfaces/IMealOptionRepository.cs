using HealthyBreakfastApp.Domain.Entities;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IMealOptionRepository
    {
        Task AddAsync(MealOption entity);
        Task SaveChangesAsync();
        Task<MealOption?> GetByIdAsync(int id);
        Task<IEnumerable<MealOption>> GetByMealIdAsync(int mealId);
    }
}
