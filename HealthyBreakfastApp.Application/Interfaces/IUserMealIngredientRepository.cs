using HealthyBreakfastApp.Domain.Entities;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IUserMealIngredientRepository
    {
        Task AddAsync(UserMealIngredient entity);
        Task SaveChangesAsync();
        Task<UserMealIngredient?> GetByIdAsync(int id);
    }
}
