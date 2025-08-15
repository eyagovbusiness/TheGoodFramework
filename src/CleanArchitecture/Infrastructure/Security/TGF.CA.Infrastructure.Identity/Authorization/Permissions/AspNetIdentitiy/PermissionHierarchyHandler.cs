using Microsoft.AspNetCore.Authorization;

namespace SF.Common.Infrastructure.Identity.Authorization.Permissions.AspNetIdentitiy {
    public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement> {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement) {
            var userClaims = context.User.FindAll(requirement.ClaimType).Select(c => c.Value).ToHashSet();

            if (requirement.RequiredValues.All(userClaims.Contains)) {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}
