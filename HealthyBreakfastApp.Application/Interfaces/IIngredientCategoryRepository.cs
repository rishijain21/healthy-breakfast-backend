using HealthyBreakfastApp.Domain.Entities;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IIngredientCategoryRepository
    {
        Task AddAsync(IngredientCategory entity);
        Task SaveChangesAsync();
        Task<IngredientCategory?> GetByIdAsync(int id);
    }
}
