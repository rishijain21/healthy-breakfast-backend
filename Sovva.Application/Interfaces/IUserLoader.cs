using Sovva.Domain.Entities;
using System;
using System.Threading.Tasks;

namespace Sovva.Application.Interfaces
{
    public interface IUserLoader
    {
        Task<User?> GetUserWithAuthMappingAsync(int userId);
    }
}
