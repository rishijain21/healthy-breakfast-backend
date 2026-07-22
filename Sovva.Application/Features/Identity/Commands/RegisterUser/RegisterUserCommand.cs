using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Identity.Commands.RegisterUser
{
    public record RegisterUserCommand(RegisterUserRequest Request) : IRequest<UserDto>;
}
