using Sovva.Application.DTOs;
using System.Threading.Tasks;

namespace Sovva.Application.Interfaces
{
    public interface IUserMealIngredientService
    {
        Task<int> CreateUserMealIngredientAsync(CreateUserMealIngredientDto dto);
        Task<UserMealIngredientDto?> GetUserMealIngredientByIdAsync(int id);
    }
}
