using HealthyBreakfastApp.Application.DTOs;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IMealOptionService
    {
        Task<int> CreateMealOptionAsync(CreateMealOptionDto dto);
        Task<MealOptionDto?> GetMealOptionByIdAsync(int id);
    }
}
