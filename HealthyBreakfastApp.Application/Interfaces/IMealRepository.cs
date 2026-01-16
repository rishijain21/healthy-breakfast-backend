using System.Threading.Tasks;
using HealthyBreakfastApp.Domain.Entities;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IMealRepository
    {
        Task AddMealAsync(Meal meal);
        Task SaveChangesAsync();
        Task<Meal?> GetByIdAsync(int id);
        Task<IEnumerable<Meal>> GetAllAsync();
        
        // NEW ADMIN METHODS
        Task<Meal?> GetByIdWithOptionsAsync(int id);
        Task UpdateMealAsync(Meal meal);
        Task DeleteMealAsync(Meal meal);
    }
}
