using HealthyBreakfastApp.Application.DTOs;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IUserService
    {
        Task<int> CreateUserAsync(CreateUserDto dto);
        Task<UserDto?> GetUserByIdAsync(int id);
    }
}
