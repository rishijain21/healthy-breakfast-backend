using System;
using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Identity.Queries.GetUserByAuthId
{
    public record GetUserByAuthIdQuery(Guid AuthId) : IRequest<UserDto?>;
}
