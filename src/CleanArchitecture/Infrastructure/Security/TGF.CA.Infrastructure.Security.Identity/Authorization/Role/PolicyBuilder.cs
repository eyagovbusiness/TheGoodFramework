using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

namespace TGF.CA.Infrastructure.Security.Identity.Authorization.Role
{
    internal static class PolicyBuilder
    {
        public static void AddRoleHierarchyPolicy(AuthorizationOptions aAuthorizationOptions, string aRole)
        {
            aAuthorizationOptions.AddPolicy($"Requires_{aRole}", policy =>
            {
                policy.AuthenticationSchemes.Add(JwtBearerDefaults.AuthenticationScheme);
                policy.RequireAuthenticatedUser();
                policy.Requirements.Add(new RoleHierarchyRequirement(aRole));
            });
        }
    }
}
