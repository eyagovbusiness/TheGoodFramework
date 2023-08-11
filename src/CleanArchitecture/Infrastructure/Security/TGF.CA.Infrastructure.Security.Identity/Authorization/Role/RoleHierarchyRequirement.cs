using Microsoft.AspNetCore.Authorization;

namespace TGF.CA.Infrastructure.Security.Identity.Authorization.Role
{
    public class RoleHierarchyRequirement : IAuthorizationRequirement
    {
        public string Role { get; }

        public RoleHierarchyRequirement(string aRole)
            => Role = aRole;
    }
}
