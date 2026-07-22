using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.DTOs;
using Sovva.Application.Features.Users;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Identity.Queries.GetUserByEmail
{
    public class GetUserByEmailQueryHandler : IRequestHandler<GetUserByEmailQuery, UserDto?>
    {
        private readonly IUserRepository _userRepository;

        public GetUserByEmailQueryHandler(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<UserDto?> Handle(GetUserByEmailQuery request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByEmailAsync(request.Email);
            if (user == null) return null;

            return UserHelper.MapToUserDto(user);
        }
    }
}
