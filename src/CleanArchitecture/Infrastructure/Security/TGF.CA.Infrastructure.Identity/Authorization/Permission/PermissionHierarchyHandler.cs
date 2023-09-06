using Microsoft.AspNetCore.Authorization;
using TGF.CA.Application;

namespace TGF.CA.Infrastructure.Security.Identity.Authorization
{
    /// <summary>
    /// Subclass of <see cref="AuthorizationHandler{T}"/> where 
    /// </summary>
    /// <typeparam name="TPermissionsEnum">The enum type that represents the permissions, the expected enum type should be defined with the attribute Flags.</typeparam>
    public class PermissionAuthorizationHandler<TPermissionsEnum> : AuthorizationHandler<PermissionRequirement<TPermissionsEnum>>
        where TPermissionsEnum : struct
    {
        /// <summary>
        /// Overrides the HandleRequirement behavior checking that the user permissions claim from the authorization cintext has the PermissionRequirement flags set.
        /// </summary>
        /// <param name="aContext"></param>
        /// <param name="aPermissionRequirement">The required permission flags to succeed in this authorization context.</param>
        /// <returns></returns>
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext aContext, PermissionRequirement<TPermissionsEnum> aPermissionRequirement)
        {
            if (int.TryParse(aContext.User.Claims.FirstOrDefault(c => c.Type == DefaultApplicationClaimTypes.Permissions)?.Value, out int lNumericValue))
                if ((lNumericValue & Convert.ToInt32(aPermissionRequirement.RequiredPermissions)) == Convert.ToInt32(aPermissionRequirement.RequiredPermissions))
                    aContext.Succeed(aPermissionRequirement);
            return Task.CompletedTask;
        }
    }
}
