using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Identity.Queries.UserExists
{
    public class UserExistsQueryHandler : IRequestHandler<UserExistsQuery, bool>
    {
        private readonly IUserRepository _userRepository;

        public UserExistsQueryHandler(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<bool> Handle(UserExistsQuery request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByEmailAsync(request.Email);
            return user != null;
        }
    }
}
