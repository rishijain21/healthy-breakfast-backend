using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Users.Commands.CreateUser
{
    public record CreateUserCommand(CreateUserDto Dto) : IRequest<int>;
}
