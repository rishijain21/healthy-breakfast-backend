using HealthyBreakfastApp.Application.DTOs;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IIngredientService
    {
        Task<int> CreateIngredientAsync(CreateIngredientDto dto);
        Task<IngredientDto?> GetIngredientByIdAsync(int id);
    }
}
