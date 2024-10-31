using Microsoft.AspNetCore.Authorization;

namespace TGF.CA.Infrastructure.Identity.Authorization.Role {
    /// <summary>
    /// Implementation of <see cref="IAuthorizationRequirement"/> that defines role requirements.
    /// </summary>
    public class RoleHierarchyRequirement : IAuthorizationRequirement {
        public string RoleName { get; }

        public RoleHierarchyRequirement(string aRoleName)
            => RoleName = aRoleName;
    }
}
