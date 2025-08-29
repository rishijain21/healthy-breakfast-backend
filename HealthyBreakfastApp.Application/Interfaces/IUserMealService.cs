using HealthyBreakfastApp.Application.DTOs;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IUserMealService
    {
        Task<int> CreateUserMealAsync(CreateUserMealDto dto);
        Task<UserMealDto?> GetUserMealByIdAsync(int id);
        Task<IEnumerable<UserMealDto>> GetUserMealsByUserIdAsync(int userId);
    }
}
