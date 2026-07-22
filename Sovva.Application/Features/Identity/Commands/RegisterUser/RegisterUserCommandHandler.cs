using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Sovva.Application.DTOs;
using Sovva.Application.Features.Users;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;

namespace Sovva.Application.Features.Identity.Commands.RegisterUser
{
    public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, UserDto>
    {
        private readonly IUserRepository _userRepository;
        private readonly IAppTimeProvider _time;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<RegisterUserCommandHandler> _logger;

        public RegisterUserCommandHandler(
            IUserRepository userRepository,
            IAppTimeProvider time,
            IUnitOfWork unitOfWork,
            ILogger<RegisterUserCommandHandler> logger)
        {
            _userRepository = userRepository;
            _time = time;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<UserDto> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
        {
            var request = command.Request;

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

            var existingUserByAuth = await _userRepository.GetUserByAuthIdIncludingDeletedAsync(request.AuthId);
            if (existingUserByAuth != null)
            {
                if (existingUserByAuth.AccountStatus == AccountStatus.Deleted)
                {
                    existingUserByAuth.AccountStatus = AccountStatus.Active;
                    existingUserByAuth.DeletedAt = null;
                    existingUserByAuth.Name = request.Name;
                    existingUserByAuth.Phone = request.Phone ?? string.Empty;
                    existingUserByAuth.UpdatedAt = _time.UtcNow;

                    await _userRepository.UpdateUserAsync(existingUserByAuth);
                    await _unitOfWork.SaveChangesAsync();

                    return UserHelper.MapToUserDto(existingUserByAuth);
                }

                throw new InvalidOperationException("User already registered with this authentication ID");
            }

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

            return UserHelper.MapToUserDto(createdUser);
        }
    }
}
