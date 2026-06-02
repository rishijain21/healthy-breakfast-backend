using Sovva.Application.DTOs;

namespace Sovva.Application.Interfaces
{
    public interface IUserService
    {
        /// <summary>
        /// Creates a new user record in the system.
        /// </summary>
        /// <param name="dto">The user creation details.</param>
        /// <returns>The internal UserId of the created user.</returns>
        Task<int> CreateUserAsync(CreateUserDto dto);

        /// <summary>
        /// Retrieves a user by their internal database ID.
        /// </summary>
        /// <param name="id">The internal UserId.</param>
        /// <returns>The user DTO or null if not found.</returns>
        Task<UserDto?> GetUserByIdAsync(int id);

        /// <summary>
        /// Retrieves a paginated list of all users in the system (Admin only).
        /// </summary>
        /// <param name="page">Page number starting from 1.</param>
        /// <param name="pageSize">Number of items per page.</param>
        /// <returns>A paged result containing user DTOs.</returns>
        Task<PagedResult<UserDto>> GetAllUsersAsync(int page = 1, int pageSize = 50);

        /// <summary>
        /// Checks if a user with the specified email already exists.
        /// </summary>
        /// <param name="email">The email address to check.</param>
        /// <returns>True if exists, false otherwise.</returns>
        Task<bool> UserExistsAsync(string email);

        /// <summary>
        /// Retrieves a user by their email address.
        /// </summary>
        /// <param name="email">The email address to lookup.</param>
        /// <returns>The user DTO or null if not found.</returns>
        Task<UserDto?> GetUserByEmailAsync(string email);

        /// <summary>
        /// Registers a new user with an initial auth mapping.
        /// </summary>
        /// <param name="request">Registration details including AuthId.</param>
        /// <returns>The created user DTO.</returns>
        Task<UserDto> RegisterUserAsync(RegisterUserRequest request);

        /// <summary>
        /// Retrieves an active user by their external authentication ID.
        /// </summary>
        /// <param name="authId">The UUID from Supabase.</param>
        /// <returns>The user DTO or null if not found or deleted.</returns>
        Task<UserDto?> GetUserByAuthIdAsync(Guid authId);

        /// <summary>
        /// Retrieves a user by their auth ID, even if their account is marked as deleted.
        /// </summary>
        /// <param name="authId">The UUID from Supabase.</param>
        /// <returns>The user DTO or null if not found.</returns>
        Task<UserDto?> GetUserByAuthIdIncludingDeletedAsync(Guid authId);
        
        /// <summary>
        /// Retrieves a user's public profile details by their auth ID.
        /// </summary>
        /// <param name="authId">The UUID from Supabase.</param>
        /// <returns>The user DTO or null if not found.</returns>
        Task<UserDto?> GetUserProfileByAuthIdAsync(Guid authId);

        /// <summary>
        /// Updates a user's profile information (name, phone, etc.).
        /// </summary>
        /// <param name="authId">The UUID of the user to update.</param>
        /// <param name="dto">The updated profile data.</param>
        /// <returns>The updated user DTO.</returns>
        Task<UserDto> UpdateUserProfileAsync(Guid authId, UpdateUserProfileDto dto);

        /// <summary>
        /// Updates a user's system role (Admin only).
        /// </summary>
        /// <param name="userId">The internal ID of the user.</param>
        /// <param name="role">The new role name.</param>
        /// <returns>True if successful, false otherwise.</returns>
        Task<bool> UpdateUserRoleAsync(int userId, string role);

        /// <summary>
        /// Marks a user account as deleted (Soft Delete).
        /// </summary>
        /// <param name="userId">The internal ID of the user to delete.</param>
        /// <returns>True if successful, false otherwise.</returns>
        Task<bool> DeleteAccountAsync(int userId);
    }
}
