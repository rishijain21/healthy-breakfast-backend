using Microsoft.AspNetCore.Mvc;
using Sovva.Application.DTOs;

namespace Sovva.WebAPI.Extensions;

public static class ControllerExtensions
{
    public static IActionResult ForbidWithLog(
        this ControllerBase controller,
        ILogger logger,
        string resource,
        long resourceId,
        long userId)
    {
        logger.LogWarning(
            "Ownership check failed: UserId={UserId} attempted access to {Resource} Id={ResourceId}",
            userId, resource, resourceId);

        return controller.StatusCode(403, 
            ApiResponse<string>.Fail("FORBIDDEN", 
                "You do not have access to this resource"));
    }
}
