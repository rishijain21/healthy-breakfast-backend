using Sovva.Application.DTOs;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Sovva.Application.Interfaces
{
    public interface IUserMealService
    {
        // ✅ SECURE: CreateUserMealAsync with userId from JWT token
        Task<int> CreateUserMealAsync(CreateUserMealDto dto, int userId);
        Task<UserMealDto?> GetUserMealByIdAsync(int id);
        Task<IEnumerable<UserMealDto>> GetUserMealsByUserIdAsync(int userId);
    }
}
