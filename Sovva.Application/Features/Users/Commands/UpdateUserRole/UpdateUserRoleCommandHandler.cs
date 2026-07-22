using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.Common.Infrastructure;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Sovva.Domain.Enums;

namespace Sovva.Application.Features.Users.Commands.UpdateUserRole
{
    public class UpdateUserRoleCommandHandler : IRequestHandler<UpdateUserRoleCommand, bool>
    {
        private readonly IUserRepository _userRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly IAppTimeProvider _time;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICacheService _cacheService;

        public UpdateUserRoleCommandHandler(
            IUserRepository userRepository,
            ICurrentUserService currentUserService,
            IAppTimeProvider time,
            IUnitOfWork unitOfWork,
            ICacheService cacheService)
        {
            _userRepository = userRepository;
            _currentUserService = currentUserService;
            _time = time;
            _unitOfWork = unitOfWork;
            _cacheService = cacheService;
        }

        public async Task<bool> Handle(UpdateUserRoleCommand request, CancellationToken cancellationToken)
        {
            var userId = request.UserId;
            var role = request.Role;

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                return false;

            if (!Enum.TryParse<UserRole>(role, ignoreCase: true, out var parsedRole))
                throw new ArgumentException($"Invalid role. Must be one of: {string.Join(", ", Enum.GetNames<UserRole>())}");

            user.Role = parsedRole;
            user.UpdatedAt = _time.UtcNow;

            await _userRepository.UpdateUserAsync(user);
            await _unitOfWork.SaveChangesAsync();

            await _currentUserService.InvalidateCacheAsync(userId);

            await _cacheService.RemoveAsync(CacheKeys.UserById(userId));
            await _cacheService.RemoveAsync(CacheKeys.DashboardProfile(userId));
            if (user.AuthMapping != null)
            {
                await _cacheService.RemoveAsync(CacheKeys.UserByAuthId(user.AuthMapping.AuthId.ToString()));
            }

            return true;
        }
    }
}
