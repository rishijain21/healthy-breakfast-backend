using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Sovva.Application.Common.Infrastructure;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Sovva.Domain.Enums;

namespace Sovva.Application.Features.Users.Commands.DeleteAccount
{
    public class DeleteAccountCommandHandler : IRequestHandler<DeleteAccountCommand, bool>
    {
        private readonly IUserRepository _userRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly IAppTimeProvider _time;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICacheService _cacheService;
        private readonly ILogger<DeleteAccountCommandHandler> _logger;

        public DeleteAccountCommandHandler(
            IUserRepository userRepository,
            ICurrentUserService currentUserService,
            IAppTimeProvider time,
            IUnitOfWork unitOfWork,
            ICacheService cacheService,
            ILogger<DeleteAccountCommandHandler> logger)
        {
            _userRepository = userRepository;
            _currentUserService = currentUserService;
            _time = time;
            _unitOfWork = unitOfWork;
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task<bool> Handle(DeleteAccountCommand request, CancellationToken cancellationToken)
        {
            var userId = request.UserId;

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return false;

            user.DeletedAt = _time.UtcNow;
            user.AccountStatus = AccountStatus.Deleted;
            user.UpdatedAt = _time.UtcNow;

            await _userRepository.UpdateUserAsync(user);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogWarning(
                "Account deleted: UserId={UserId} Email={Email} DeletedAt={DeletedAt}",
                user.UserId, user.Email, user.DeletedAt);

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
