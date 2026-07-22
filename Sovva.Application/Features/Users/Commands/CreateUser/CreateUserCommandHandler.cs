using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;

namespace Sovva.Application.Features.Users.Commands.CreateUser
{
    public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, int>
    {
        private readonly IUserRepository _userRepository;
        private readonly IAppTimeProvider _time;
        private readonly IUnitOfWork _unitOfWork;

        public CreateUserCommandHandler(
            IUserRepository userRepository,
            IAppTimeProvider time,
            IUnitOfWork unitOfWork)
        {
            _userRepository = userRepository;
            _time = time;
            _unitOfWork = unitOfWork;
        }

        public async Task<int> Handle(CreateUserCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Dto;

            var user = new User
            {
                Name = dto.Name,
                Email = dto.Email.ToLower(),
                Phone = dto.Phone,
                AccountStatus = AccountStatus.Active,
                Role = UserRole.Customer,
                CreatedAt = _time.UtcNow,
                UpdatedAt = _time.UtcNow
            };

            await _userRepository.AddUserAsync(user);
            await _unitOfWork.SaveChangesAsync();
            return user.UserId;
        }
    }
}
