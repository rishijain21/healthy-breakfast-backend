using Sovva.Application.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sovva.Application.Interfaces
{
    public interface IIngredientService
    {
        // Read operations
        Task<IEnumerable<IngredientDto>> GetAllIngredientsAsync();
        Task<IEnumerable<IngredientDto>> GetIngredientsByCategoryIdAsync(int categoryId);
        Task<IngredientDto?> GetIngredientByIdAsync(int id);
        
        // Create operations
        Task<int> CreateIngredientAsync(CreateIngredientDto dto);
        
        // Update operations
        Task<bool> UpdateIngredientAsync(int id, UpdateIngredientDto dto);
        Task<bool> ToggleIngredientAvailabilityAsync(int id);
        
        // Delete operations
        Task<bool> DeleteIngredientAsync(int id);
    }
}
