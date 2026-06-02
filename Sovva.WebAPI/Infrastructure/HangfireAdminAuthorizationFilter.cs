using Hangfire.Dashboard;
using Microsoft.AspNetCore.Http;
using Sovva.Domain.Constants;

namespace Sovva.WebAPI.Infrastructure;

/// <summary>
/// Authorization filter for Hangfire dashboard that requires Admin role via JWT.
/// </summary>
public class HangfireAdminAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // Check if user is authenticated
        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
            return false;

        // Check if user has Admin role claim
        return httpContext.User.HasClaim(RoleConstants.SovvaRole, RoleConstants.Admin);
    }
}