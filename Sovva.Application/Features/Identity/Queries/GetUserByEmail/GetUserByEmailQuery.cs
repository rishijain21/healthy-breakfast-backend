using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Identity.Queries.GetUserByEmail
{
    public record GetUserByEmailQuery(string Email) : IRequest<UserDto?>;
}
