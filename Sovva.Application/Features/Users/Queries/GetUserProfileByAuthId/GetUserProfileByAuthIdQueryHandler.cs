using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.Common.Infrastructure;
using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Users.Queries.GetUserProfileByAuthId
{
    public class GetUserProfileByAuthIdQueryHandler : IRequestHandler<GetUserProfileByAuthIdQuery, UserDto?>
    {
        private readonly IUserRepository _userRepository;
        private readonly ICacheService _cacheService;

        public GetUserProfileByAuthIdQueryHandler(
            IUserRepository userRepository,
            ICacheService cacheService)
        {
            _userRepository = userRepository;
            _cacheService = cacheService;
        }

        public async Task<UserDto?> Handle(GetUserProfileByAuthIdQuery request, CancellationToken cancellationToken)
        {
            var authId = request.AuthId;
            var cacheKey = CacheKeys.UserByAuthId(authId.ToString());
            var cached = await _cacheService.GetAsync<UserDto>(cacheKey);
            if (cached != null) return cached;

            var user = await _userRepository.GetUserByAuthIdAsync(authId);
            if (user == null) return null;

            var result = UserHelper.MapToUserDto(user);
            await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(10));
            return result;
        }
    }
}
