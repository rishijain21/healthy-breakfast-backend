using HealthyBreakfastApp.Domain.Entities;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IUserMealRepository
    {
        Task AddAsync(UserMeal entity);
        Task SaveChangesAsync();
        Task<UserMeal?> GetByIdAsync(int id);
        Task<IEnumerable<UserMeal>> GetByUserIdAsync(int userId);
    }
}
