using HealthyBreakfastApp.Domain.Entities;
using System;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IUserLoader
    {
        Task<User?> GetUserWithAuthMappingAsync(int userId);
    }
}
