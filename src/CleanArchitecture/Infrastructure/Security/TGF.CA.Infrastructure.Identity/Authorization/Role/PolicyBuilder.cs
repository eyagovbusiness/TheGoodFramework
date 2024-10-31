using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

namespace TGF.CA.Infrastructure.Identity.Authorization.Role {
    /// <summary>
    /// Static class that provides logic to add role hierarchy policies.
    /// </summary>
    internal static class RolePolicyBuilder {
        /// <summary>
        /// Adds role hierarchy based authorization policies to the provided AuthorizationOptions.
        /// </summary>
        /// <param name="aAuthorizationOptions"><see cref="AuthorizationOptions"/> where to add the role policies.</param>
        /// <param name="aRoleName">Role name that is required to satisfy the role policy requirement.</param>
        public static void AddRoleHierarchyPolicy(AuthorizationOptions aAuthorizationOptions, string aRoleName) {
            aAuthorizationOptions.AddPolicy($"Requires_{aRoleName}", policy => {
                policy.AuthenticationSchemes.Add(JwtBearerDefaults.AuthenticationScheme);
                policy.RequireAuthenticatedUser();
                policy.Requirements.Add(new RoleHierarchyRequirement(aRoleName));
            });
        }
    }
}
