using Sovva.Domain.Entities;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Sovva.Application.Interfaces
{
    public interface IUserMealRepository
    {
        Task AddAsync(UserMeal entity);
        Task SaveChangesAsync();
        Task<UserMeal?> GetByIdAsync(int id);
        Task<IEnumerable<UserMeal>> GetByUserIdAsync(int userId);
        Task<List<UserMeal>> GetByIdsAsync(List<int> userMealIds);
        
        // ✅ NEW: Get UserMeal by UserId and MealId (for auto-find-or-create logic)
        Task<UserMeal?> GetByUserIdAndMealIdAsync(int userId, int mealId);
    }
}
