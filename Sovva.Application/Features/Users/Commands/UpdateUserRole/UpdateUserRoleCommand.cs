using MediatR;

namespace Sovva.Application.Features.Users.Commands.UpdateUserRole
{
    public record UpdateUserRoleCommand(int UserId, string Role) : IRequest<bool>;
}
