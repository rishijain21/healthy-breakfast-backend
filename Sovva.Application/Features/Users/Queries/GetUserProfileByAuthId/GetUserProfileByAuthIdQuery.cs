using System;
using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Users.Queries.GetUserProfileByAuthId
{
    public record GetUserProfileByAuthIdQuery(Guid AuthId) : IRequest<UserDto?>;
}
