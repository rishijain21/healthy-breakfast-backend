using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.Application.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IUserRepository _userRepository;

        public CurrentUserService(
            IHttpContextAccessor httpContextAccessor,
            IUserRepository userRepository)
        {
            _httpContextAccessor = httpContextAccessor;
            _userRepository = userRepository;
        }

        // Retrieves the auth_id from the HTTP context
        public string? GetAuthId()
        {
            return _httpContextAccessor.HttpContext?.Items["auth_id"]?.ToString();
        }

        // Returns the currently logged-in user's UserId
        public async Task<int?> GetCurrentUserIdAsync()
        {
            var authId = GetAuthId();
            if (string.IsNullOrEmpty(authId))
                return null;

            if (!Guid.TryParse(authId, out var authGuid))
                return null;

            var user = await _userRepository.GetUserByAuthIdAsync(authGuid);
            return user?.UserId;
        }

        // Returns the currently logged-in user's details as UserDto
        public async Task<UserDto?> GetCurrentUserAsync()
        {
            var authId = GetAuthId();
            if (string.IsNullOrEmpty(authId))
                return null;

            if (!Guid.TryParse(authId, out var authGuid))
                return null;

            var user = await _userRepository.GetUserByAuthIdAsync(authGuid);
            if (user == null)
                return null;

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
    }
}
