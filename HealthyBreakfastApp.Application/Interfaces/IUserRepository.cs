using HealthyBreakfastApp.Domain.Entities;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IUserRepository
    {
        Task AddUserAsync(User user);
        Task SaveChangesAsync();
        Task<User?> GetByIdAsync(int id);
    }
}
