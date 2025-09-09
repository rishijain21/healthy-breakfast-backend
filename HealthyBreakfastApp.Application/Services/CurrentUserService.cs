using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Domain.Entities;
using System;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.Application.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IUserRepository _userRepository;

        public CurrentUserService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<UserDto?> GetCurrentUserAsync(Guid authId)
        {
            // We'll implement this using a new repository method
            var user = await _userRepository.GetByAuthIdAsync(authId);
            
            if (user == null) return null;

            return new UserDto
            {
                UserId = user.UserId,
                Name = user.Name,
                Email = user.Email,
                Phone = user.Phone,
                WalletBalance = user.WalletBalance,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };
        }

        public async Task<int?> GetCurrentUserIdAsync(Guid authId)
        {
            var user = await _userRepository.GetByAuthIdAsync(authId);
            return user?.UserId;
        }
    }
}
