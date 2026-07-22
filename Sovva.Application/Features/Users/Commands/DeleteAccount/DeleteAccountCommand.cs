using MediatR;

namespace Sovva.Application.Features.Users.Commands.DeleteAccount
{
    public record DeleteAccountCommand(int UserId) : IRequest<bool>;
}
