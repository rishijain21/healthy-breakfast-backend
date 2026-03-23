using System.Security.Claims;

namespace Sovva.WebAPI.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static int? GetSovvaUserId(this ClaimsPrincipal user)
    {
        var claim = user.FindFirst("sovva_user_id")?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }

    public static string? GetSovvaRole(this ClaimsPrincipal user)
        => user.FindFirst("sovva_role")?.Value;

    public static Guid? GetAuthId(this ClaimsPrincipal user)
    {
        var claim = user.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}