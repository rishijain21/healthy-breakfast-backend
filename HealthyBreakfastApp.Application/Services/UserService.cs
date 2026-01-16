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

        // ✅ EXISTING METHODS (updated to include new fields)
        public async Task<int> CreateUserAsync(CreateUserDto dto)
        {
            var user = new User
            {
                Name = dto.Name,
                Email = dto.Email,
                Phone = dto.Phone,
                WalletBalance = 0,
                AccountStatus = "Active",
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

            return MapToUserDto(user);
        }

        public async Task<bool> UserExistsAsync(string email)
        {
            var user = await _userRepository.GetByEmailAsync(email);
            return user != null;
        }

        public async Task<List<UserDto>> GetAllUsersAsync()
        {
            var users = await _userRepository.GetAllAsync();
            return users.Select(MapToUserDto).ToList();
        }

        public async Task<UserDto?> GetUserByEmailAsync(string email)
        {
            var user = await _userRepository.GetByEmailAsync(email);
            if (user == null) return null;

            return MapToUserDto(user);
        }

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
                AccountStatus = "Active",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var createdUser = await _userRepository.CreateUserWithAuthMappingAsync(user, request.AuthId);

            return MapToUserDto(createdUser);
        }

        public async Task<UserDto?> GetUserByAuthIdAsync(Guid authId)
        {
            var user = await _userRepository.GetUserByAuthIdAsync(authId);
            if (user == null) return null;

            return MapToUserDto(user);
        }

        // ✅ NEW: Get user profile by AuthId (for profile page)
        public async Task<UserDto?> GetUserProfileByAuthIdAsync(Guid authId)
        {
            var user = await _userRepository.GetUserByAuthIdAsync(authId);
            if (user == null) return null;

            return MapToUserDto(user);
        }

        // ✅ NEW: Update user profile
        public async Task<UserDto> UpdateUserProfileAsync(Guid authId, UpdateUserProfileDto dto)
        {
            var user = await _userRepository.GetUserByAuthIdAsync(authId);
            if (user == null)
            {
                throw new InvalidOperationException("User not found");
            }

            // Update only provided fields (partial update)
            if (!string.IsNullOrWhiteSpace(dto.Name))
            {
                user.Name = dto.Name.Trim();
            }

            if (dto.Phone != null) // Allow empty string to clear phone
            {
                user.Phone = dto.Phone.Trim();
            }

            if (dto.DeliveryAddress != null) // Allow empty string to clear address
            {
                user.DeliveryAddress = dto.DeliveryAddress.Trim();
            }

            user.UpdatedAt = DateTime.UtcNow;

            await _userRepository.UpdateUserAsync(user);
            await _userRepository.SaveChangesAsync();

            return MapToUserDto(user);
        }

        // ✅ HELPER: Map User entity to UserDto
        private UserDto MapToUserDto(User user)
        {
            return new UserDto
            {
                UserId = user.UserId,
                Name = user.Name,
                Email = user.Email,
                Phone = user.Phone,
                DeliveryAddress = user.DeliveryAddress,
                AccountStatus = user.AccountStatus,
                WalletBalance = user.WalletBalance,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                ProfileComplete = !string.IsNullOrWhiteSpace(user.Name) &&
                                !string.IsNullOrWhiteSpace(user.Phone) &&
                                !string.IsNullOrWhiteSpace(user.DeliveryAddress)
            };
        }
    }
}
