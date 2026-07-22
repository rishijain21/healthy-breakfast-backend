using System;
using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Identity.Queries.GetUserByAuthIdIncludingDeleted
{
    public record GetUserByAuthIdIncludingDeletedQuery(Guid AuthId) : IRequest<UserDto?>;
}
