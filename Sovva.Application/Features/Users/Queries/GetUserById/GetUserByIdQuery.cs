using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Users.Queries.GetUserById
{
    public record GetUserByIdQuery(int UserId) : IRequest<UserDto?>;
}
