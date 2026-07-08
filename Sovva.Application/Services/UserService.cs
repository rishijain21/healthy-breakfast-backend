using Sovva.Application.DTOs;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Sovva.Application.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly IAppTimeProvider _time;
        private readonly ILogger<UserService> _logger;
        private readonly ICacheService _cacheService;

        private const string UserByIdCacheKeyPrefix = "user:id:";
        private const string UserByAuthIdCacheKeyPrefix = "user:auth:";

        public UserService(
            IUserRepository userRepository, 
            ICurrentUserService currentUserService,
            IAppTimeProvider time,
            ILogger<UserService> logger,
            ICacheService cacheService)
        {
            _userRepository = userRepository;
            _currentUserService = currentUserService;
            _time = time;
            _logger = logger;
            _cacheService = cacheService;
        }

        // ✅ EXISTING METHODS (updated to include new fields)
        public async Task<int> CreateUserAsync(CreateUserDto dto)
        {
            var user = new User
            {
                Name = dto.Name,
                Email = dto.Email.ToLower(),
                Phone = dto.Phone,
                AccountStatus = AccountStatus.Active,
                Role = UserRole.Customer,
                CreatedAt = _time.UtcNow,
                UpdatedAt = _time.UtcNow
            };

            await _userRepository.AddUserAsync(user);
            await _userRepository.SaveChangesAsync();
            return user.UserId;
        }

        public async Task<UserDto?> GetUserByIdAsync(int id)
        {
            var cacheKey = UserByIdCacheKeyPrefix + id;
            var cached = await _cacheService.GetAsync<UserDto>(cacheKey);
            if (cached != null) return cached;

            var user = await _userRepository.GetByIdAsync(id);
            if (user == null) return null;

            var result = MapToUserDto(user);
            await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(10));
            return result;
        }

        public async Task<bool> UserExistsAsync(string email)
        {
            var user = await _userRepository.GetByEmailAsync(email);
            return user != null;
        }

        public async Task<PagedResult<UserDto>> GetAllUsersAsync(int page = 1, int pageSize = 50)
        {
            var users = await _userRepository.GetAllAsync(page, pageSize);
            var totalCount = await _userRepository.CountAsync();
            
            return new PagedResult<UserDto>
            {
                Items = users.Select(MapToUserDto).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<UserDto?> GetUserByEmailAsync(string email)
        {
            var user = await _userRepository.GetByEmailAsync(email);
            if (user == null) return null;

            return MapToUserDto(user);
        }

        public async Task<UserDto> RegisterUserAsync(RegisterUserRequest request)
        {
            // Check if email already exists
            var existingUserByEmail = await _userRepository.GetByEmailAsync(request.Email);
            if (existingUserByEmail != null)
            {
                if (existingUserByEmail.AccountStatus == AccountStatus.Deleted)
                {
                    // Case: User was deleted, now registering again with same email
                    // We'll re-activate them in the next step when we check AuthId
                }
                else
                {
                    throw new InvalidOperationException("Email already registered");
                }
            }

            // Check if user already exists with this AuthId (including deleted)
            var existingUserByAuth = await _userRepository.GetUserByAuthIdIncludingDeletedAsync(request.AuthId);
            if (existingUserByAuth != null)
            {
                if (existingUserByAuth.AccountStatus == AccountStatus.Deleted)
                {
                    // RE-ACTIVATE DELETED USER
                    existingUserByAuth.AccountStatus = AccountStatus.Active;
                    existingUserByAuth.DeletedAt = null;
                    existingUserByAuth.Name = request.Name;
                    existingUserByAuth.Phone = request.Phone ?? string.Empty;
                    existingUserByAuth.UpdatedAt = _time.UtcNow;

                    await _userRepository.UpdateUserAsync(existingUserByAuth);
                    await _userRepository.SaveChangesAsync();

                    return MapToUserDto(existingUserByAuth);
                }

                throw new InvalidOperationException("User already registered with this authentication ID");
            }

            // Create new user with auth mapping
            var user = new User
            {
                Name = request.Name,
                Email = request.Email.ToLower(),
                Phone = request.Phone ?? string.Empty,
                AccountStatus = AccountStatus.Active,
                Role = UserRole.Customer,
                CreatedAt = _time.UtcNow,
                UpdatedAt = _time.UtcNow
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

        public async Task<UserDto?> GetUserByAuthIdIncludingDeletedAsync(Guid authId)
        {
            var user = await _userRepository.GetUserByAuthIdIncludingDeletedAsync(authId);
            if (user == null) return null;

            return MapToUserDto(user);
        }

        // ✅ NEW: Get user profile by AuthId (for profile page)
        public async Task<UserDto?> GetUserProfileByAuthIdAsync(Guid authId)
        {
            var cacheKey = UserByAuthIdCacheKeyPrefix + authId;
            var cached = await _cacheService.GetAsync<UserDto>(cacheKey);
            if (cached != null) return cached;

            var user = await _userRepository.GetUserByAuthIdAsync(authId);
            if (user == null) return null;

            var result = MapToUserDto(user);
            await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(10));
            return result;
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

            // DeliveryAddress removed — managed via UserAddresses table

            user.UpdatedAt = _time.UtcNow;

            await _userRepository.UpdateUserAsync(user);
            await _userRepository.SaveChangesAsync();

            await _cacheService.RemoveAsync(UserByIdCacheKeyPrefix + user.UserId);
            await _cacheService.RemoveAsync(UserByAuthIdCacheKeyPrefix + authId);
            await _cacheService.RemoveAsync("dashboard:profile:" + user.UserId);

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
                AccountStatus = user.AccountStatus.ToString(),
                // WalletBalance omitted — always use GET /api/WalletTransactions/my-balance
                Role = user.Role.ToString(),
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                IsProfileComplete = !string.IsNullOrWhiteSpace(user.Name) &&
                                !string.IsNullOrWhiteSpace(user.Phone)
            };
        }

        // ✅ ADMIN: Update user role
        public async Task<bool> UpdateUserRoleAsync(int userId, string role)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                return false;

            if (!Enum.TryParse<UserRole>(role, ignoreCase: true, out var parsedRole))
                throw new ArgumentException($"Invalid role. Must be one of: {string.Join(", ", Enum.GetNames<UserRole>())}");

            user.Role = parsedRole;
            user.UpdatedAt = _time.UtcNow;

            await _userRepository.UpdateUserAsync(user);
            await _userRepository.SaveChangesAsync();

            await _currentUserService.InvalidateCacheAsync(userId);

            await _cacheService.RemoveAsync(UserByIdCacheKeyPrefix + userId);
            await _cacheService.RemoveAsync("dashboard:profile:" + userId);
            if (user.AuthMapping != null)
            {
                await _cacheService.RemoveAsync(UserByAuthIdCacheKeyPrefix + user.AuthMapping.AuthId);
            }

            return true;
        }

        public async Task<bool> DeleteAccountAsync(int userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return false;

            user.DeletedAt = _time.UtcNow;
            user.AccountStatus = AccountStatus.Deleted;
            user.UpdatedAt = _time.UtcNow;

            await _userRepository.UpdateUserAsync(user);
            await _userRepository.SaveChangesAsync();

            _logger.LogWarning(
                "Account deleted: UserId={UserId} Email={Email} DeletedAt={DeletedAt}",
                user.UserId, user.Email, user.DeletedAt);

            await _currentUserService.InvalidateCacheAsync(userId);

            await _cacheService.RemoveAsync(UserByIdCacheKeyPrefix + userId);
            await _cacheService.RemoveAsync("dashboard:profile:" + userId);
            if (user.AuthMapping != null)
            {
                await _cacheService.RemoveAsync(UserByAuthIdCacheKeyPrefix + user.AuthMapping.AuthId);
            }

            return true;
        }
    }
}
