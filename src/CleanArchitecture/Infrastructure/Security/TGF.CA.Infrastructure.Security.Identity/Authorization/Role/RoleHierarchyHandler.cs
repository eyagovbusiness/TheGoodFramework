using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace TGF.CA.Infrastructure.Security.Identity.Authorization.Role
{
    /// <summary>
    /// Authorization handler that determines if the user has the required role
    /// </summary>
    public class RoleHierarchyHandler : AuthorizationHandler<RoleHierarchyRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext aContext, RoleHierarchyRequirement aRequirement)
        {
            var lUserRoleClaim = aContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value!;
            var lRoleHierarchyList = new List<string> {"Admin", "Espada", "Daga", "Cadete", "Afiliado"};

            if (lRoleHierarchyList.IndexOf(aRequirement.Role) != -1 && lRoleHierarchyList.IndexOf(lUserRoleClaim) <= lRoleHierarchyList.IndexOf(aRequirement.Role))
                aContext.Succeed(aRequirement);

            return Task.CompletedTask;
        }

    }
}
