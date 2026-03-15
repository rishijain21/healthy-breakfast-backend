using Sovva.Application.DTOs;
using System.Threading.Tasks;

namespace Sovva.Application.Interfaces
{
    public interface IMealOptionService
    {
        Task<int> CreateMealOptionAsync(CreateMealOptionDto dto);
        // ✅ REMOVED: Task<MealOptionDto?> GetMealOptionByIdAsync(int id);
    }
}
