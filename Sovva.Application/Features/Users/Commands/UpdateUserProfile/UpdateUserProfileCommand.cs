using System;
using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Users.Commands.UpdateUserProfile
{
    public record UpdateUserProfileCommand(Guid AuthId, UpdateUserProfileDto Dto) : IRequest<UserDto>;
}
