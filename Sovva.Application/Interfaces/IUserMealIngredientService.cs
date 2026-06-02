using Sovva.Application.DTOs;
using System.Threading.Tasks;

namespace Sovva.Application.Interfaces
{
    public interface IUserMealIngredientService
    {
        Task<int> CreateUserMealIngredientAsync(CreateUserMealIngredientDto dto);
        Task CreateUserMealIngredientsAsync(IEnumerable<CreateUserMealIngredientDto> dtos);
        Task<UserMealIngredientDto?> GetUserMealIngredientByIdAsync(int id);
    }
}
