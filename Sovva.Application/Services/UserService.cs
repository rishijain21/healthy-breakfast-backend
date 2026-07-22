using System;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.DTOs;
using Sovva.Application.Features.Identity.Commands.RegisterUser;
using Sovva.Application.Features.Identity.Queries.GetUserByAuthId;
using Sovva.Application.Features.Identity.Queries.GetUserByAuthIdIncludingDeleted;
using Sovva.Application.Features.Identity.Queries.GetUserByEmail;
using Sovva.Application.Features.Identity.Queries.UserExists;
using Sovva.Application.Features.Users.Commands.CreateUser;
using Sovva.Application.Features.Users.Commands.DeleteAccount;
using Sovva.Application.Features.Users.Commands.UpdateUserProfile;
using Sovva.Application.Features.Users.Commands.UpdateUserRole;
using Sovva.Application.Features.Users.Queries.GetAllUsers;
using Sovva.Application.Features.Users.Queries.GetUserById;
using Sovva.Application.Features.Users.Queries.GetUserProfileByAuthId;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Services
{
    public class UserService : IUserService
    {
        private readonly ISender _sender;

        public UserService(ISender sender)
        {
            _sender = sender;
        }

        public async Task<int> CreateUserAsync(CreateUserDto dto)
        {
            return await _sender.Send(new CreateUserCommand(dto));
        }

        public async Task<UserDto?> GetUserByIdAsync(int id)
        {
            return await _sender.Send(new GetUserByIdQuery(id));
        }

        public async Task<PagedResult<UserDto>> GetAllUsersAsync(int page = 1, int pageSize = 50)
        {
            return await _sender.Send(new GetAllUsersQuery(page, pageSize));
        }

        public async Task<bool> UserExistsAsync(string email)
        {
            return await _sender.Send(new UserExistsQuery(email));
        }

        public async Task<UserDto?> GetUserByEmailAsync(string email)
        {
            return await _sender.Send(new GetUserByEmailQuery(email));
        }

        public async Task<UserDto> RegisterUserAsync(RegisterUserRequest request)
        {
            return await _sender.Send(new RegisterUserCommand(request));
        }

        public async Task<UserDto?> GetUserByAuthIdAsync(Guid authId)
        {
            return await _sender.Send(new GetUserByAuthIdQuery(authId));
        }

        public async Task<UserDto?> GetUserByAuthIdIncludingDeletedAsync(Guid authId)
        {
            return await _sender.Send(new GetUserByAuthIdIncludingDeletedQuery(authId));
        }

        public async Task<UserDto?> GetUserProfileByAuthIdAsync(Guid authId)
        {
            return await _sender.Send(new GetUserProfileByAuthIdQuery(authId));
        }

        public async Task<UserDto> UpdateUserProfileAsync(Guid authId, UpdateUserProfileDto dto)
        {
            return await _sender.Send(new UpdateUserProfileCommand(authId, dto));
        }

        public async Task<bool> UpdateUserRoleAsync(int userId, string role)
        {
            return await _sender.Send(new UpdateUserRoleCommand(userId, role));
        }

        public async Task<bool> DeleteAccountAsync(int userId)
        {
            return await _sender.Send(new DeleteAccountCommand(userId));
        }
    }
}
