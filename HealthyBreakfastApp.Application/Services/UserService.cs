using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Domain.Entities;

namespace HealthyBreakfastApp.Application.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;

        public UserService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        // ✅ EXISTING METHODS (unchanged)
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
            return user != null;
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

        // ✅ Get user by email (returns DTO)
        public async Task<UserDto?> GetUserByEmailAsync(string email)
        {
            var user = await _userRepository.GetByEmailAsync(email);
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

        // ✅ Register user with AuthId mapping (only way to create users)
        public async Task<UserDto> RegisterUserAsync(RegisterUserRequest request)
        {
            // Check if user already exists with this AuthId
            var existingUser = await _userRepository.GetUserByAuthIdAsync(request.AuthId);
            if (existingUser != null)
            {
                throw new InvalidOperationException("User already registered with this authentication ID");
            }

            // Check if email already exists
            var emailExists = await _userRepository.GetByEmailAsync(request.Email);
            if (emailExists != null)
            {
                throw new InvalidOperationException("Email already registered");
            }

            // Create new user with auth mapping
            var user = new User
            {
                Name = request.Name,
                Email = request.Email,
                Phone = request.Phone ?? string.Empty,
                WalletBalance = 0.00m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var createdUser = await _userRepository.CreateUserWithAuthMappingAsync(user, request.AuthId);

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

        // ✅ Get user by Supabase AuthId (only finds, doesn't create)
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

        // ❌ REMOVED: FindOrCreateUserByAuthIdAsync() - This was causing auto-creation bug
    }
}
