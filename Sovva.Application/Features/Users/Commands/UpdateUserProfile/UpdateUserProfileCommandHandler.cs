using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.Common.Infrastructure;
using Sovva.Application.DTOs;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Users.Commands.UpdateUserProfile
{
    public class UpdateUserProfileCommandHandler : IRequestHandler<UpdateUserProfileCommand, UserDto>
    {
        private readonly IUserRepository _userRepository;
        private readonly IAppTimeProvider _time;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICacheService _cacheService;

        public UpdateUserProfileCommandHandler(
            IUserRepository userRepository,
            IAppTimeProvider time,
            IUnitOfWork unitOfWork,
            ICacheService cacheService)
        {
            _userRepository = userRepository;
            _time = time;
            _unitOfWork = unitOfWork;
            _cacheService = cacheService;
        }

        public async Task<UserDto> Handle(UpdateUserProfileCommand request, CancellationToken cancellationToken)
        {
            var authId = request.AuthId;
            var dto = request.Dto;

            var user = await _userRepository.GetUserByAuthIdAsync(authId);
            if (user == null)
            {
                throw new InvalidOperationException("User not found");
            }

            if (!string.IsNullOrWhiteSpace(dto.Name))
            {
                user.Name = dto.Name.Trim();
            }

            if (dto.Phone != null)
            {
                user.Phone = dto.Phone.Trim();
            }

            user.UpdatedAt = _time.UtcNow;

            await _userRepository.UpdateUserAsync(user);
            await _unitOfWork.SaveChangesAsync();

            await _cacheService.RemoveAsync(CacheKeys.UserById(user.UserId));
            await _cacheService.RemoveAsync(CacheKeys.UserByAuthId(authId.ToString()));
            await _cacheService.RemoveAsync(CacheKeys.DashboardProfile(user.UserId));

            return UserHelper.MapToUserDto(user);
        }
    }
}
