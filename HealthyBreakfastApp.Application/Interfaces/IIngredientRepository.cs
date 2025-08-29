using System.Threading.Tasks;
using HealthyBreakfastApp.Domain.Entities;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IIngredientRepository
    {
        Task AddIngredientAsync(Ingredient ingredient);
        Task SaveChangesAsync();
        Task<Ingredient?> GetByIdAsync(int id);
        Task<Ingredient?> GetByIdWithCategoryAsync(int id); // ADD THIS LINE
    }
}
