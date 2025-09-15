using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Domain.Entities;
using System;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.Application.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;

        public UserService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        // ✅ YOUR EXISTING METHODS (unchanged)
        public async Task<int> CreateUserAsync(CreateUserDto dto)
        {
            var user = new User
            {
                Name = dto.Name,
                Email = dto.Email,
                Phone = dto.Phone,
                WalletBalance = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _userRepository.AddUserAsync(user);
            await _userRepository.SaveChangesAsync();
            return user.UserId;
        }

        public async Task<UserDto?> GetUserByIdAsync(int id)
        {
            var user = await _userRepository.GetByIdAsync(id);
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

        public async Task<bool> UserExistsAsync(string email)
        {
            var user = await _userRepository.GetByEmailAsync(email);
            return user != null && !string.IsNullOrEmpty(user.Name);
        }

        public async Task<List<UserDto>> GetAllUsersAsync()
        {
            var users = await _userRepository.GetAllAsync();
            
            return users.Select(user => new UserDto
            {
                UserId = user.UserId,
                Name = user.Name,
                Email = user.Email,
                Phone = user.Phone,
                WalletBalance = user.WalletBalance,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            }).ToList();
        }

        public async Task<UserDto> RegisterUserAsync(RegisterUserRequest request)
        {
            var user = new User
            {
                Name = request.Name,
                Email = request.Email,
                Phone = request.Phone ?? string.Empty,
                WalletBalance = 0.00m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            
            await _userRepository.AddUserAsync(user);
            await _userRepository.SaveChangesAsync();
            
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

        // ✅ ADD THESE NEW METHODS
        public async Task<UserDto?> GetUserByAuthIdAsync(Guid authId)
        {
            var user = await _userRepository.GetUserByAuthIdAsync(authId);
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

        public async Task<UserDto> FindOrCreateUserByAuthIdAsync(Guid authId, string? name = null, string? email = null)
        {
            // Try to find existing user
            var existingUser = await _userRepository.GetUserByAuthIdAsync(authId);
            if (existingUser != null)
            {
                return new UserDto
                {
                    UserId = existingUser.UserId,
                    Name = existingUser.Name,
                    Email = existingUser.Email,
                    Phone = existingUser.Phone,
                    WalletBalance = existingUser.WalletBalance,
                    CreatedAt = existingUser.CreatedAt,
                    UpdatedAt = existingUser.UpdatedAt
                };
            }

            // Create new user if not found
            var user = new User
            {
                Name = name ?? "New User",
                Email = email ?? "",
                Phone = "",
                WalletBalance = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var createdUser = await _userRepository.CreateUserWithAuthMappingAsync(user, authId);
            
            return new UserDto
            {
                UserId = createdUser.UserId,
                Name = createdUser.Name,
                Email = createdUser.Email,
                Phone = createdUser.Phone,
                WalletBalance = createdUser.WalletBalance,
                CreatedAt = createdUser.CreatedAt,
                UpdatedAt = createdUser.UpdatedAt
            };
        }
    }
}
