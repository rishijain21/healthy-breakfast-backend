using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.Common.Infrastructure;
using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Users.Queries.GetUserById
{
    public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, UserDto?>
    {
        private readonly IUserRepository _userRepository;
        private readonly ICacheService _cacheService;

        public GetUserByIdQueryHandler(
            IUserRepository userRepository,
            ICacheService cacheService)
        {
            _userRepository = userRepository;
            _cacheService = cacheService;
        }

        public async Task<UserDto?> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
        {
            var id = request.UserId;
            var cacheKey = CacheKeys.UserById(id);
            var cached = await _cacheService.GetAsync<UserDto>(cacheKey);
            if (cached != null) return cached;

            var user = await _userRepository.GetByIdAsync(id);
            if (user == null) return null;

            var result = UserHelper.MapToUserDto(user);
            await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(10));
            return result;
        }
    }
}
