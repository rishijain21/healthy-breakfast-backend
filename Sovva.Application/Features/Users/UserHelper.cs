using Sovva.Application.DTOs;
using Sovva.Domain.Entities;

namespace Sovva.Application.Features.Users
{
    public static class UserHelper
    {
        public static UserDto MapToUserDto(User user)
        {
            return new UserDto
            {
                UserId = user.UserId,
                Name = user.Name,
                Email = user.Email,
                Phone = user.Phone,
                AccountStatus = user.AccountStatus.ToString(),
                Role = user.Role.ToString(),
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                IsProfileComplete = !string.IsNullOrWhiteSpace(user.Name) &&
                                !string.IsNullOrWhiteSpace(user.Phone)
            };
        }
    }
}
