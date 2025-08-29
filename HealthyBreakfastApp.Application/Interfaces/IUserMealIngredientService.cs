using HealthyBreakfastApp.Application.DTOs;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IUserMealIngredientService
    {
        Task<int> CreateUserMealIngredientAsync(CreateUserMealIngredientDto dto);
        Task<UserMealIngredientDto?> GetUserMealIngredientByIdAsync(int id);
    }
}
