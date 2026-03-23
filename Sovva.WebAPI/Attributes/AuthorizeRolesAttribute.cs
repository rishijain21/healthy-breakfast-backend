using Microsoft.AspNetCore.Authorization;

namespace Sovva.WebAPI.Attributes
{
    /// <summary>
    /// Shorthand for [Authorize(Roles = "...")] — works because
    /// Program.cs maps RoleClaimType to "sovva_role" from JWT.
    /// </summary>
    public class AuthorizeRolesAttribute : AuthorizeAttribute
    {
        public AuthorizeRolesAttribute(params string[] roles)
        {
            Roles = string.Join(",", roles);
        }
    }
}