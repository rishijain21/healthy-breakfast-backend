using MediatR;

namespace Sovva.Application.Features.Identity.Queries.UserExists
{
    public record UserExistsQuery(string Email) : IRequest<bool>;
}
