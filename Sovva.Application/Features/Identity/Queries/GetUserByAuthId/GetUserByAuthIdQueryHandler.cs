using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.DTOs;
using Sovva.Application.Features.Users;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Identity.Queries.GetUserByAuthId
{
    public class GetUserByAuthIdQueryHandler : IRequestHandler<GetUserByAuthIdQuery, UserDto?>
    {
        private readonly IUserRepository _userRepository;

        public GetUserByAuthIdQueryHandler(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<UserDto?> Handle(GetUserByAuthIdQuery request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetUserByAuthIdAsync(request.AuthId);
            if (user == null) return null;

            return UserHelper.MapToUserDto(user);
        }
    }
}
