using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Users.Queries.GetAllUsers
{
    public record GetAllUsersQuery(int Page = 1, int PageSize = 50) : IRequest<PagedResult<UserDto>>;
}
