using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.DTOs;
using Sovva.Application.Features.Users;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Identity.Queries.GetUserByAuthIdIncludingDeleted
{
    public class GetUserByAuthIdIncludingDeletedQueryHandler : IRequestHandler<GetUserByAuthIdIncludingDeletedQuery, UserDto?>
    {
        private readonly IUserRepository _userRepository;

        public GetUserByAuthIdIncludingDeletedQueryHandler(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<UserDto?> Handle(GetUserByAuthIdIncludingDeletedQuery request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetUserByAuthIdIncludingDeletedAsync(request.AuthId);
            if (user == null) return null;

            return UserHelper.MapToUserDto(user);
        }
    }
}
