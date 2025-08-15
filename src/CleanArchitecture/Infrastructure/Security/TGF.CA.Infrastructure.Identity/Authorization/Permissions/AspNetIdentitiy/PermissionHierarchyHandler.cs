using Microsoft.AspNetCore.Authorization;

namespace SF.Common.Infrastructure.Identity.Authorization.Permissions.AspNetIdentitiy {
    /// <summary>
    /// Authorization handler that validates whether a user possesses all required permission claims.
    /// </summary>
    public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement> {
        /// <summary>
        /// Evaluates the current authorization context to determine whether the user meets the <see cref="PermissionRequirement"/>.
        /// </summary>
        /// <param name="context">The authorization context.</param>
        /// <param name="requirement">The required permission set.</param>
        /// <returns>A completed <see cref="Task"/> representing the authorization check.</returns>
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
