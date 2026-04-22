using System.Threading.Tasks;
using Sovva.Domain.Entities;

namespace Sovva.Application.Interfaces
{
    public interface IMealRepository
    {
        // ✅ Public method for meal builder
        Task<IEnumerable<Meal>> GetActiveMealsAsync();

        Task AddMealAsync(Meal meal);
        Task SaveChangesAsync();
        Task<Meal?> GetByIdAsync(int id);
        Task<IEnumerable<Meal>> GetAllAsync();
        
        // NEW ADMIN METHODS
        Task<Meal?> GetByIdWithOptionsAsync(int id);
        Task UpdateMealAsync(Meal meal);
        Task<bool> UpdateMealStatusAsync(int id, bool isComplete);
        Task DeleteMealAsync(Meal meal);

        // ✅ NEW: Paginated admin list
        Task<(IEnumerable<Meal> Items, int TotalCount)> GetPagedAsync(int page, int pageSize);

        // ✅ NEW: Get all meals with options loaded (fixes N+1)
        Task<IEnumerable<Meal>> GetAllWithOptionsCountAsync();

        // ✅ NEW: Batch fetch for users (single query with IsComplete filter)
        Task<List<Meal>> GetByIdsForUsersAsync(List<int> ids);
    }
}
