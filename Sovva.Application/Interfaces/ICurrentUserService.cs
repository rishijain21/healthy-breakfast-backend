using Sovva.Application.DTOs;
using System.Threading.Tasks;

namespace Sovva.Application.Interfaces
{
    public interface ICurrentUserService
    {
        /// <summary>
        /// Gets the unique external authentication identifier (UUID) from the current JWT token.
        /// </summary>
        /// <returns>The AuthId string or null if not authenticated.</returns>
        string? GetAuthId();

        /// <summary>
        /// Retrieves the full profile of the currently logged-in user, including internal ID and role.
        /// </summary>
        /// <returns>User profile DTO or null if not found.</returns>
        Task<UserDto?> GetCurrentUserAsync();

        /// <summary>
        /// Gets the internal integer database ID for the current authenticated user.
        /// </summary>
        /// <returns>The UserId or null if not authenticated.</returns>
        Task<int?> GetCurrentUserIdAsync();

        /// <summary>
        /// Clears the cached user profile from memory, forcing a fresh database load on the next request.
        /// </summary>
        /// <param name="userId">The internal ID of the user whose cache should be cleared.</param>
        Task InvalidateCacheAsync(int userId);
    }
}
